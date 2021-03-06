﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using ToastNotifications;

namespace SlackWindowsTray
{
    public partial class MainWindow : Form
    {
        // Used for easy access to UIThread
        private static Form _form;
        public static Form Form { get { return _form; } }

        private StateService _stateService = StateService.Instance;
        private SlackState _lastSlackState;

        public MainWindow()
        {
            _form = this;

            InitializeComponent();
            slackTrayIcon.ContextMenuStrip = trayContextMenu;

            _stateService.OnStateChange += (o, state) => this.UIThread(delegate { ChangeSlackState(state); });
            _stateService.OnSnoozeFinished += StateServiceOnOnSnoozeFinished;

            _stateService.Start();
            RtmConnection.Instance.Start();

            ChangeSlackState(new SlackState(TrayStates.DisconnectedFromExtension));
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            // Add the notifier to Windows startup:
            try
            {
                RegistryKey currentVersionRunRegKey = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                string startPath = Assembly.GetExecutingAssembly().Location;
                currentVersionRunRegKey.SetValue("SlackWindowsTray", '"' + startPath + '"');
                this.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to add SlackWindowsTray to run on startup: " + ex.Message);
            }
        }

        private void ChangeSlackState(SlackState slackState)
        {
            _lastSlackState = slackState;
            // Change the icon and the tooltip
            slackTrayIcon.Text = slackState.TrayState.ToString();

            using (var stream = this.GetType().Assembly.GetManifestResourceStream("SlackWindowsTray.Icons." + slackState.TrayState + ".ico"))
            {
                slackTrayIcon.Icon = new Icon(stream);
            }
        }

        private void slackTrayIcon_DoubleClick(object sender, EventArgs e)
        {
            var activated = ChromeActivator.ActivateChromeWindowByTitle(window => window.Title.EndsWith(" Slack"));
            if (!activated)
            {
                MessageBox.Show("Couldn't find Slack window");
            }
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ShowHideSnoozeMenuItems(bool snoozeVisible)
        {
            snoozeStripMenuItem.Visible = snoozeVisible;
            unsnoozeStripMenuItem.Visible = !snoozeVisible;
        }

        private async void snoozeStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHideSnoozeMenuItems(snoozeVisible: false);
            _stateService.Snooze();
        }

        private void unsnoozeStripMenuItem_Click(object sender, EventArgs e)
        {
            _stateService.Unsnooze();
        }

        
        private void StateServiceOnOnSnoozeFinished(object sender, EventArgs eventArgs)
        {
            ShowHideSnoozeMenuItems(snoozeVisible: true);
        }

        private EventHandler getChatSnoozeItemClickEvent(ChatState chatState)
        {
            return delegate
            {
                ShowHideSnoozeMenuItems(snoozeVisible: false);
                _stateService.Snooze(chatState.name);
            };
        }


        private void trayContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            const string CHAT_SNOOZE_SEPERATOR = "ChatSnoozeSeperator";

            // Delete old snooze menu items
            var chatSnoozeMenuItems = (from contextItem in trayContextMenu.Items.Cast<ToolStripItem>()
                                       where (string)contextItem.Tag == CHAT_SNOOZE_SEPERATOR
                                       select contextItem).ToList();

            foreach (var chatSnoozeMenuItem in chatSnoozeMenuItems)
            {
                trayContextMenu.Items.Remove(chatSnoozeMenuItem);
            }

            // Create new snooze menu items
            if (_lastSlackState != null)
            {
                var newMenuItems = (from chatState in _lastSlackState.ChatStates
                                    where chatState.unread
                                    orderby chatState.name
                                    let menuName = "Snooze " + chatState.name
                                    let clickEvent = getChatSnoozeItemClickEvent(chatState)
                                    select new ToolStripMenuItem(menuName, null, clickEvent) { Tag = CHAT_SNOOZE_SEPERATOR }
                                   ).ToList();

                if (newMenuItems.Any())
                {
                    trayContextMenu.Items.Insert(0, new ToolStripSeparator { Tag = CHAT_SNOOZE_SEPERATOR });

                    foreach (var menuItem in newMenuItems)
                    {
                        trayContextMenu.Items.Insert(0, menuItem);
                    }
                }
            }
        }

        private void OptionsStripMenuItem_Click(object sender, EventArgs e)
        {
            OptionsForm form = new OptionsForm();
            form.ShowDialog();
            _stateService.Refresh();
        }
    }
}