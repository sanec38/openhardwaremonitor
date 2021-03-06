﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace OpenHardwareMonitor.GUI
{
    public class ShowDesktop
    {
        public delegate void ShowDesktopChangedEventHandler(bool showDesktop);

        private readonly NativeWindow referenceWindow;

        private readonly string referenceWindowCaption =
            "OpenHardwareMonitorShowDesktopReferenceWindow";

        private bool showDesktop;

        private readonly Timer timer;

        private ShowDesktop()
        {
            // create a reference window to detect show desktop
            referenceWindow = new NativeWindow();
            var cp = new CreateParams();
            cp.ExStyle = GadgetWindow.WS_EX_TOOLWINDOW;
            cp.Caption = referenceWindowCaption;
            referenceWindow.CreateHandle(cp);
            NativeMethods.SetWindowPos(referenceWindow.Handle,
                GadgetWindow.HWND_BOTTOM, 0, 0, 0, 0, GadgetWindow.SWP_NOMOVE |
                                                      GadgetWindow.SWP_NOSIZE | GadgetWindow.SWP_NOACTIVATE |
                                                      GadgetWindow.SWP_NOSENDCHANGING);

            // start a repeated timer to detect "Show Desktop" events 
            timer = new Timer(OnTimer, null,
                Timeout.Infinite, Timeout.Infinite);
        }

        public static ShowDesktop Instance { get; } = new ShowDesktop();

        private event ShowDesktopChangedEventHandler ShowDesktopChangedEvent;

        private void StartTimer()
        {
            timer.Change(0, 200);
        }

        private void StopTimer()
        {
            timer.Change(Timeout.Infinite,
                Timeout.Infinite);
        }

        // the desktop worker window (if available) can hide the reference window
        private IntPtr GetDesktopWorkerWindow()
        {
            var shellWindow = NativeMethods.GetShellWindow();
            if (shellWindow == IntPtr.Zero)
                return IntPtr.Zero;

            int shellId;
            NativeMethods.GetWindowThreadProcessId(shellWindow, out shellId);

            var workerWindow = IntPtr.Zero;
            while ((workerWindow = NativeMethods.FindWindowEx(
                IntPtr.Zero, workerWindow, "WorkerW", null)) != IntPtr.Zero)
            {
                int workerId;
                NativeMethods.GetWindowThreadProcessId(workerWindow, out workerId);
                if (workerId == shellId)
                {
                    var window = NativeMethods.FindWindowEx(
                        workerWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (window != IntPtr.Zero)
                    {
                        var desktopWindow = NativeMethods.FindWindowEx(
                            window, IntPtr.Zero, "SysListView32", null);
                        if (desktopWindow != IntPtr.Zero)
                            return workerWindow;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void OnTimer(object state)
        {
            bool showDesktopDetected;

            var workerWindow = GetDesktopWorkerWindow();
            if (workerWindow != IntPtr.Zero)
            {
                // search if the reference window is behind the worker window
                var reference = NativeMethods.FindWindowEx(
                    IntPtr.Zero, workerWindow, null, referenceWindowCaption);
                showDesktopDetected = reference != IntPtr.Zero;
            }
            else
            {
                // if there is no worker window, then nothing can hide the reference
                showDesktopDetected = false;
            }

            if (showDesktop != showDesktopDetected)
            {
                showDesktop = showDesktopDetected;
                if (ShowDesktopChangedEvent != null)
                {
                    ShowDesktopChangedEvent(showDesktop);
                }
            }
        }

        // notify when the "show desktop" mode is changed
        public event ShowDesktopChangedEventHandler ShowDesktopChanged
        {
            add
            {
                // start the monitor timer when someone is listening
                if (ShowDesktopChangedEvent == null)
                    StartTimer();
                ShowDesktopChangedEvent += value;
            }
            remove
            {
                ShowDesktopChangedEvent -= value;
                // stop the monitor timer if nobody is interested
                if (ShowDesktopChangedEvent == null)
                    StopTimer();
            }
        }

        private static class NativeMethods
        {
            private const string USER = "user32.dll";

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern bool SetWindowPos(IntPtr hWnd,
                IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr FindWindowEx(IntPtr hwndParent,
                IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern IntPtr GetShellWindow();

            [DllImport(USER, CallingConvention = CallingConvention.Winapi)]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd,
                out int processId);
        }
    }
}