﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.ComponentModel;



namespace ARSMonitor
{


    ///////////////////////////////////////////////////////
    ///////// pinging hosts
    ///////////////////////////////////////////////////////

    public class networkProtocol
    {
        public List<serverControl> serverList;

        public class CurrentState
        {
            public bool isOnline;
            public string address;
        }

        public bool workState = true;

        public void serialPingServers(BackgroundWorker worker,
                                        DoWorkEventArgs e,
                                        int[] speeds
            )
        {
            while (!worker.CancellationPending)
            {
                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();
                options.DontFragment = true;
                string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; // сделать размер буфера изменяемым в настройках
                CurrentState state = new CurrentState();
                byte[] buffer = Encoding.ASCII.GetBytes(data); // сделать изменяемым в настройках
                int timeout = speeds[2]; // сделать изменяемым в настройках
                double count = serverList.Count;
                double i = 0;
                int progress;
                foreach (serverControl server in serverList)
                {
                    System.Threading.Thread.Sleep(speeds[0]);
                    if (worker.CancellationPending)
                    {
                        break;
                    }
                    i++;
                    progress = (int)Math.Round((i / count) * 100);

                    string host = server.objectAddress; // сделать изменяемым в настройках
                    PingReply reply = pingSender.Send(host, timeout, buffer, options);
                    if (reply.Status == IPStatus.Success)
                    {
                        state.address = server.objectAddress;
                        state.isOnline = true;
                        worker.ReportProgress(progress, state);
                    }
                    else
                    {
                        state.address = server.objectAddress;
                        state.isOnline = false;
                        worker.ReportProgress(progress, state);
                    }
                }
                if (!workState)
                    System.Threading.Thread.Sleep(1000);
                System.Threading.Thread.Sleep(speeds[1]);
            }
            e.Cancel = true;
        }


        public void parallelPingServers(System.ComponentModel.BackgroundWorker worker,
                                        System.ComponentModel.DoWorkEventArgs e,
                                        int[] speeds,
                                        ARSMonitor.serverControl server
            )
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            options.DontFragment = true;
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"; // сделать размер буфера изменяемым в настройках
            CurrentState state = new CurrentState();
            byte[] buffer = Encoding.ASCII.GetBytes(data); // сделать изменяемым в настройках
            int timeout = 120; // сделать изменяемым в настройках
            double count = serverList.Count;
            double i = 0;
            int progress;
            i++;
            progress = (int)Math.Round((i / count) * 100);
            //elements.Find(el => el.Child == server);
            string host = server.objectAddress; // сделать изменяемым в настройках

            while (!worker.CancellationPending)
            {
                PingReply reply = pingSender.Send(host, timeout, buffer, options);
                if (reply.Status == IPStatus.Success)
                {
                    state.address = server.objectAddress;
                    state.isOnline = true;
                    /*while (worker.IsBusy)
                        System.Threading.Thread.Sleep(100);*/
                    worker.ReportProgress(progress, state);
                }
                else
                {
                    state.address = server.objectAddress;
                    state.isOnline = false;
                    worker.ReportProgress(progress, state);
                }
                if (!workState)
                    System.Threading.Thread.Sleep(1000);

                System.Threading.Thread.Sleep(speeds[1]);
            }
            e.Cancel = true;
        }
    }
}