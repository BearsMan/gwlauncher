﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GWCA.Memory;
using System.IO;
using Newtonsoft.Json;
using GWMC_CS;

namespace GW_Launcher
{


    static class Program
    {

        const string gwlMutexName = "gwl_instance_mutex";
        public static AccountManager accounts;
        public static Thread mainthread;
        public static Mutex mutex = new Mutex();
        public static Mutex gwlMutex;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        extern static IntPtr FindWindowW(string clsname, string winname);

        [DllImport("user32.dll")]
        extern static bool SetForegroundWindow(IntPtr hWNd);

        [STAThread]
        static void Main()
        {

            if (Mutex.TryOpenExisting(gwlMutexName, out gwlMutex))
            {
                return;
            }
            else
            {
                gwlMutex = new Mutex(true, gwlMutexName);
            }


            accounts = new AccountManager("Accounts.json");
            for (int i = 0; i < accounts.Length; ++i)
            {
                accounts[i].active = false;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm mf = new MainForm();
            mf.Location = new System.Drawing.Point(-1000, -1000);

            mainthread = new Thread(() =>
            {
                bool main_closed = false;
                mf.FormClosed += (s, a) => { main_closed = true; };
                while (!main_closed)
                {
                    int sleep = 5000;
                    while (mf.needtolaunch.Count > 0)
                    {
                        int i = mf.needtolaunch.Dequeue();
                        Account a = accounts[i];
                        GWCAMemory m = MulticlientPatch.LaunchClient(a.gwpath, " -email \"" + a.email + "\" -password \"" + a.password + "\" -character \"" + a.character + "\" " + a.extraargs, a.datfix, false, a.mods);
                        GWMem.FindAddressesIfNeeded(m);
                        a.process = m;
                        m.WriteWString(GWMem.WinTitle, a.character + '\0');

                        mf.SetActive(i, true);

                        while (m.Read<ushort>(GWMem.CharnamePtr) == 0)
                            Thread.Sleep(1000);

                        Thread.Sleep(sleep);
                        sleep += 5000;
                    }


                    mutex.WaitOne();

                    for(int i = 0; i < accounts.Length; ++i)
                    {
                        if (accounts[i].active)
                        if (accounts[i].process.process.HasExited)
                        {
                            mf.SetActive(i, false);
                        }
                    }

                    mutex.ReleaseMutex();

                    Thread.Sleep(150);
                }
            });
            Application.Run(mf);

            mainthread.Abort();
        }
    }
}
