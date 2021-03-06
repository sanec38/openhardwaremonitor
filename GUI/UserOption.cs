﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Windows.Forms;

namespace OpenHardwareMonitor.GUI
{
    public class UserOption
    {
        private readonly MenuItem menuItem;
        private readonly string name;
        private readonly PersistentSettings settings;
        private bool value;

        public UserOption(string name, bool value,
            MenuItem menuItem, PersistentSettings settings)
        {
            this.settings = settings;
            this.name = name;
            if (name != null)
                this.value = settings.GetValue(name, value);
            else
                this.value = value;
            this.menuItem = menuItem;
            this.menuItem.Checked = this.value;
            this.menuItem.Click += menuItem_Click;
        }

        public bool Value
        {
            get { return value; }
            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    if (name != null)
                        settings.SetValue(name, value);
                    menuItem.Checked = value;
                    if (changed != null)
                        changed(this, null);
                }
            }
        }

        private event EventHandler changed;

        private void menuItem_Click(object sender, EventArgs e)
        {
            Value = !Value;
        }

        public event EventHandler Changed
        {
            add
            {
                changed += value;
                if (changed != null)
                    changed(this, null);
            }
            remove { changed -= value; }
        }
    }
}