﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Formatters.Soap;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace ARSMonitor
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            toolStripProgressBar1.Visible = false;
            toolStripStatusLabel2.Visible = false;
            directotries[0] = System.IO.Directory.GetCurrentDirectory();
            speed1 = 30;
            speed2 = 500;

            initOptions();
            o = new Options(this);
            expEdit = new MonitorExplorerEditor(this);
            importingServers();
            refreshControlView();
        }

        private void refreshControlView()
        {
            foreach (serverControl server in servers)
            {
                server.objectStatus = true;
                server.objectStatus = false;
            }
        }



        logNet log;
        AsynchronousSocketListener listen;
        MonitorExplorerEditor expEdit;

        void initOptions() // базовая инициализация настроек программы при запуске.
        {
            int f = 0;
            // файл опций жёстко структурирован
            string[] lines = System.IO.File.ReadAllLines(@"C:\ARSMonitor\options.ini");
            servPath = lines[0];
            if (!Int32.TryParse(lines[1], out speed1))
            {
                f = 1;
            }
            if (!Int32.TryParse(lines[2], out speed2))
            {
                f = 2;
            }
            if (!Int32.TryParse(lines[3], out speed3))
            {
                f = 3;
            }

            if (lines[4] == "1")
            {
                isParallel = true;
            }
            else if (lines[4] == "0")
            {
                isParallel = false;
            }
            else
            {
                f = 4;
            }

            picON = lines[5];
            picOFF = lines[6];

            if (f != 0)
                toolStripStatusLabel1.Text = "Options initialization failed on string number " + f.ToString() + "!";
            else toolStripStatusLabel1.Text = "Options initialized successfully";
        }



        // Опции. Константы и переменные опций.
        Options o;
        const string FileName = @"C:\ARSMonitor\CommandList.xml";
        string[] directotries = new string[3];
        public int speed1, speed2, speed3; // задержки при выполнении
        public bool isParallel = false; // переключатель типа обхода
        public string servPath = @"C:\ARSMonitor\Servers";
        public string picON, picOFF; // путь к картинкам

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            networkProtocol np = new networkProtocol();
            np.serverList = servers;
            if (!backgroundWorker1.IsBusy)
            {
                backgroundWorker1.RunWorkerAsync(np);
                toolStripProgressBar1.Visible = true;
                toolStripStatusLabel2.Text = "Waiting for ping";
                toolStripStatusLabel2.Visible = true;
            }
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPinging();
        }

        private void StopPinging() // останавливаем опрос хостов
        {
            if (backgroundWorker1.IsBusy)
                backgroundWorker1.CancelAsync();

            if (backgroundWorker2.IsBusy)
                backgroundWorker2.CancelAsync();
            threadList.ForEach(cancelWork);
            servers.ForEach(switchOff);
            cancel = true;
        }

        // метод, создающий событие отмены для работающих фоновых процессов.
        void cancelWork(BackgroundWorker bw)
        {
            if (bw.IsBusy)
                bw.CancelAsync();
        }

        // выключатель компонента.
        void switchOff(serverControl sC)
        {
            sC.objectStatus = false;
        }


        public List<ContextCommands> commands = new List<ContextCommands>(); // список комманд
        List<BackgroundWorker> threadList = new List<BackgroundWorker>(); // запуск ветки процесса на каждый хост.
        public List<serverControl> servers = new List<serverControl>(); // список хостов
        public int x = 25, y = 25;
        public bool working = true;
        public string n = "New";
        public string a = "192.168.0.4";
        public bool success = false;
        public bool cancel = false;

        private void addServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addServByMenu();
        }

        private void addServByMenu()
        {
            working = false;
            ServerConstructor f = new ServerConstructor(this);
            f.ShowDialog();
            // если форма опций
            if (success)
            {
                addingServ(servers, n, a);
                n = "New";
                a = "192.168.0.4";
                drawServers();

                working = true;
            }
            else MessageBox.Show("Adding server cancelled. ");
        }

        private void addingServ(List<serverControl> servL, string n, string a)
        {
            // добавление уникального хоста в список для проверки
            serverControl tempS = new serverControl(n, a);
            if (!servL.Exists(x => x.objectAddress == a))
            {
                tempS.ContextMenuStrip = contextMenuStrip1;
                servL.Add(tempS);
            }
            else
            {   // сообщение о неуникальности
                tempS = servL.Find(x => x.objectAddress == a);
                MessageBox.Show("Уже существует хост с таким адресом. В программе имеет имя: " + tempS.objectName);
            }
        }

        private void drawServers()
        {
            // отображение (перерисовка) контролов на форме

            x = 25;
            y = 25;

            // заполняется сначала видимое пространство слева направо, сверху вниз.
            foreach (serverControl server in servers)
            {
                panel1.Controls.Remove(server);
                server.picktOnPath(picON);
                server.picktOffPath(picOFF);
                server.SetBounds(x, y, 200, 48);

                if (x + 405 > this.Width - 25)
                {
                    x = 25;
                    y += 50;
                }
                else x += 205;

                panel1.Controls.Add(server);
                //if (y + 50 > this.Height) {  }

            }

        }

        private void drawToolStripMenuItem_Click(object sender, EventArgs e)
        {
            drawServers();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // остановка работы
            StopPinging();
            // записать текущий список хостов в файл
            exportingServers();
            // закрытие главной формы
            Close();
        }


        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // import servers list from file
            // импорт списка серверов из файла
            importingServers();
        }

        private void importingServers()
        {
            // import servers list from file
            // импорт списка серверов из файла
            string[] lines = System.IO.File.ReadAllLines(servPath);
            foreach (string serv in lines)
            {
                if (serv != "")
                {
                    string[] splitted = serv.Split(' ');
                    n = splitted[1];
                    a = splitted[0];
                    addingServ(servers, n, a);
                }
            }
            /*
            servers.ForEach(delegate(serverControl serv)
            {
                serv.ContextMenuStrip = contextMenuStrip1;
            });*/
            drawServers();
        }


        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // export servers list into file
            // экспорт списка серверов в файл
            exportingServers();
        }

        private void exportingServers()
        {
            // export servers list into file
            // экспорт списка серверов в файл
            List<string> exportString = new List<string>();
            int i = 0;
            foreach (serverControl server in servers)
            {
                exportString.Add(server.objectAddress + " " + server.objectName + Environment.NewLine);
                i++;
            }
            //if ()

            System.IO.File.WriteAllLines(servPath, exportString.ToArray<string>());
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            int[] speeds = { speed1, speed2, speed3 };
            System.ComponentModel.BackgroundWorker worker;
            worker = (System.ComponentModel.BackgroundWorker)sender;
            networkProtocol np = (networkProtocol)e.Argument;
            np.workState = working;
            np.serialPingServers(worker, e, speeds);
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            networkProtocol.CurrentState state = (networkProtocol.CurrentState)e.UserState;
            toolStripProgressBar1.Value = e.ProgressPercentage;
            string isOn;
            if (state.isOnline)
                isOn = "online";
            else isOn = "OFFLINE!!!";
            toolStripStatusLabel2.Text = state.address + " is " + isOn + "...";
            toolStripStatusLabel1.Text = "Working. ";
            serverControl server = servers.Find(x => x.objectAddress == state.address);
            if (server.objectStatus != state.isOnline)
            {
                server.objectStatus = state.isOnline;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {   // 
            if (e.Error != null)
            {
                toolStripStatusLabel1.Text = "ERROR! ";
                MessageBox.Show("Error: " + e.Error.Message);
            }
            else if (e.Cancelled)
                toolStripStatusLabel1.Text = "Work cancelled. ";
            else
                toolStripStatusLabel1.Text = "Work Finished. ";
            //MessageBox.Show("Finished counting words.");
            ;
        }

        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            if (log != null)
            {
                log.Top = this.Top;
                log.Left = this.Left + this.Width;
            }
            drawServers();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            o.ShowDialog();
        }


        public bool workState = true;


        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {   // Параллельный поток для отдельного хоста (ускорение прохода по списку хостов).
            int[] speeds = { speed1, speed2, speed3 };
            System.ComponentModel.BackgroundWorker worker;
            worker = (System.ComponentModel.BackgroundWorker)sender;
            var myArgs = e.Argument as MyWorkerArgs;
            serverControl serv = myArgs.srv;
            networkProtocol np = myArgs.netP;
            workState = working;
            np.workState = working;
            np.parallelPingServers(worker, e, speeds, serv);

        }

        private void serialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            serialPinging();
        }

        private void serialPinging()
        {
            cancel = false;
            networkProtocol np = new networkProtocol();
            np.serverList = servers;
            if (!backgroundWorker1.IsBusy)
            {
                backgroundWorker1.RunWorkerAsync(np);
                toolStripProgressBar1.Visible = true;
                toolStripStatusLabel2.Text = "Waiting for serial ping";
                toolStripStatusLabel2.Visible = true;
            }
        }
        class MyWorkerArgs
        {
            public serverControl srv;
            public networkProtocol netP;
            public MyWorkerArgs(serverControl server, networkProtocol np)
            {
                srv = server;
                netP = np;
            }
        }

        private void parallelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parallelPinging();
        }

        private void parallelPinging()
        {
            cancel = false;
            networkProtocol np = new networkProtocol();
            np.serverList = servers;
            foreach (serverControl server in servers)
            {
                threadList.Add(new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true });
                threadList.Last().DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker2_DoWork);
                threadList.Last().ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker2_ProgressChanged);
                threadList.Last().RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker2_RunWorkerCompleted);
                threadList.Last().RunWorkerAsync(new MyWorkerArgs(server, np));
                toolStripProgressBar1.Visible = true;
                toolStripStatusLabel2.Text = "Waiting for parallel ping";
                toolStripStatusLabel2.Visible = true;
                System.Threading.Thread.Sleep(speed1);
            }
        }

        private void backgroundWorker2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            networkProtocol.CurrentState state = (networkProtocol.CurrentState)e.UserState;
            toolStripProgressBar1.Value = e.ProgressPercentage;
            string isOn;
            if (state.isOnline)
                isOn = "online";
            else isOn = "OFFLINE!!!";
            toolStripStatusLabel2.Text = state.address + " is " + isOn + "...";
            toolStripStatusLabel1.Text = "Working. ";
            serverControl server = servers.Find(x => x.objectAddress == state.address);
            if (server.objectStatus != state.isOnline)
            {
                server.objectStatus = state.isOnline;
            }
        }

        private void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                toolStripStatusLabel1.Text = "ERROR! ";
                MessageBox.Show("Error: " + e.Error.Message);
            }
            else if (e.Cancelled)
                toolStripStatusLabel1.Text = "Work cancelled. ";
            else
                toolStripStatusLabel1.Text = "Work Finished. ";
            //MessageBox.Show("Finished counting words.");
            ;
        }

        private void переименоватьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            serverControl serv = contextMenuStrip1.SourceControl as serverControl;
            serv.editMode = true;
        }


        public event EventHandler<SocketEventArgs> eventFromMainForm;
        public void sendCommand(string target, string programm, string parameters)
        {
            Thread.Sleep(2);
            IPAddress addr = IPAddress.Parse(target);
            Task.Factory.StartNew((Action)delegate
            {
                if (eventFromMainForm != null)
                {
                    eventFromMainForm(this, new SocketEventArgs(target, programm, parameters));
                }
            });
        }

        public void sendCommand(string target, string programm, string[] lines)
        {
            Thread.Sleep(2);
            IPAddress addr = IPAddress.Parse(target);
            Task.Factory.StartNew((Action)delegate
            {
                if (eventFromMainForm != null)
                {
                    eventFromMainForm(this, new SocketEventArgs(target, programm, lines));
                }
            });
        }

        private void перезагрузитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            serverControl serv = contextMenuStrip1.SourceControl as serverControl;
            beforeSending(serv, sender);
            // shutdown.exe", "-r -t 0
            //Process p = Process.Start(@"cmd.exe", @"/c shutdown /r /m " + hN + " & pause");
        }


        private void beforeSending(serverControl serv, object sender)
        {
            sendCommand(serv.objectAddress, "shutdown.exe", "-r -t 0");
        }

        private static string getHostName(serverControl serv)
        {
            IPAddress hostIPAddress = IPAddress.Parse(serv.objectAddress);
            IPHostEntry hostInfo = Dns.GetHostEntry(hostIPAddress);
            return hostInfo.HostName;
        }

        private void Form1_Click(object sender, EventArgs e)
        {
            this.Focus();
        }

        private void panel1_Click(object sender, EventArgs e)
        {
            panel1.Focus();
        }

        private void connectToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (isParallel)
                parallelPinging();
            else serialPinging();
        }

        private void удалитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            serverControl serv = contextMenuStrip1.SourceControl as serverControl;
            deleteHostFromList(serv);
        }

        private void deleteHostFromList(serverControl srv)
        {
            string message = "Вы уверены, что хотите удалить этот элемент?";
            string caption = "Удалить " + srv.objectName;
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult result;
            result = MessageBox.Show(message, caption, buttons);
            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                srv.Dispose();
                servers.Remove(srv);
            }
            drawServers();
        }

        private void listenConnectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            log = new logNet();
            listen = new AsynchronousSocketListener(this);
            //networkProtocol np = new networkProtocol();
            //np.serverList = servers;
            if (!backgroundWorker3.IsBusy)
            {
                backgroundWorker3.RunWorkerAsync(listen);
                /* toolStripProgressBar1.Visible = true;
                 toolStripStatusLabel2.Text = "Waiting for serial ping";
                 toolStripStatusLabel2.Visible = true;*/

                log.StartPosition = FormStartPosition.Manual;
                log.Top = this.Top;
                log.Left = this.Left + this.Width;
                log.Show();
            }
            else MessageBox.Show("Not listening");
        }

        private void backgroundWorker3_DoWork(object sender1, DoWorkEventArgs e1)
        {
            AsynchronousSocketListener listen = e1.Argument as AsynchronousSocketListener;


            System.ComponentModel.BackgroundWorker worker;
            worker = (System.ComponentModel.BackgroundWorker)sender1;

            //e.Argument


            listen.eventFromNetworkClass += delegate(object sender, NetworkEventArgs e)
            {
                log.textBox1.Invoke((Action)delegate
                {
                    log.textBox1.AppendText(e.Message);
                    log.textBox1.AppendText(Environment.NewLine);
                });
            };
            listen.StartListening(worker, e1);
        }

        private void backgroundWorker3_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {

        }

        private void stopListeningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            backgroundWorker3.CancelAsync();
            log.Close();
        }


        private void backgroundWorker3_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string str = e.UserState as string;
            toolStripStatusLabel1.Text = str;
            log.textBox1.Text += str + Environment.NewLine + Environment.NewLine;
        }

        private void MainForm_Move(object sender, EventArgs e)
        {
            if (log != null)
            {
                log.Top = this.Top;
                log.Left = this.Left + this.Width;
            }
        }

        private void MainForm_Activated(object sender, EventArgs e)
        {
            // log.Activate();
        }

        private void sendCommandToAllToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void добавитьКнопкуToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addCommandButton();
        }


        private void addCommandButton()
        {
            //
            expEdit = new MonitorExplorerEditor(this);
            expEdit.Show();
        }

        public void addCommandButton1(object sender, EventArgs e)
        {
            //
            ToolStripMenuItem cms = sender as ToolStripMenuItem;
            ContextCommands cmd = new ContextCommands();
            //MessageBox.Show(cms.Name);
            serverControl serv = contextMenuStrip1.SourceControl as serverControl;
            cmd = commands.Find(x => x.commTS == cms);
            //cmd = sender as ContextCommands;

            MessageBox.Show(cmd.commName);
            if (cmd.Multi) sendCommand(serv.objectAddress, cmd.commProgramm, cmd.commLines);
            else sendCommand(serv.objectAddress, cmd.commProgramm, cmd.commParams);
        }

        private void добавитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            addCommandButton();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveOptions();
            saveCommands();
        }

        public void saveCommands()
        {
            // export servers list into file
            // экспорт списка серверов в файл
            List<string> exportCommands = new List<string>();
            List<ContextCommands> tmp = new List<ContextCommands>();
            //int i = 0;
            foreach (ContextCommands comm in commands)
            {
                if (comm.Multi) exportCommands.Add(comm.commName + " " + comm.commProgramm + " " + comm.commLines + Environment.NewLine);
                else exportCommands.Add(comm.commName + " " + comm.commProgramm + " " + comm.commParams + Environment.NewLine);

            }
            //if ()
            tmp = commands;
            System.IO.File.WriteAllLines(@"C:\ARSMonitor\commands.dat", exportCommands.ToArray<string>());
            //System.IO.File.WriteAllLines(@"C:\ARSMonitor\commands", tmp.ToArray<ContextCommands>());



            //Stream TestFileStream = File.Create(FileName);
            //BinaryFormatter serializer = new BinaryFormatter();
            //SoapFormatter serializer = new SoapFormatter();
            //serializer.Serialize(TestFileStream, commands);
            //TestFileStream.Close();

            XmlTextWriter xw = new XmlTextWriter(FileName, Encoding.UTF8);
            //а это чтобы красиво было :)
            xw.Formatting = Formatting.Indented;
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(xw);
            //DataContractSerializer serializer = new DataContractSerializer(typeof(commandClassCollection));
            DataContractSerializer serializer = new DataContractSerializer(typeof(List<ContextCommands>));
            //commandClassCollection cCC = new commandClassCollection();
            //cCC.Collection = commands;
            serializer.WriteObject(writer, commands);
            writer.Close();
            xw.Close();
        }

        public void saveOptions()
        {
            int f = 0;
            // файл опций жёстко структурирован
            //string[] lines;


            List<string> exportOptionsString = new List<string>();
            //int i = 0;
            exportOptionsString.Add(servPath);
            exportOptionsString.Add(speed1.ToString());
            exportOptionsString.Add(speed2.ToString());
            exportOptionsString.Add(speed3.ToString());
            if (isParallel == true)
            {
                exportOptionsString.Add("1");
            }
            else exportOptionsString.Add("0");
            exportOptionsString.Add(picON);
            exportOptionsString.Add(picOFF);

            System.IO.File.WriteAllLines(@"C:\ARSMonitor\options.ini", exportOptionsString.ToArray<string>());

            if (f != 0)
                toolStripStatusLabel1.Text = "Options initialization failed on string number " + f.ToString() + "!";
            else toolStripStatusLabel1.Text = "Options initialized successfully";
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (File.Exists(FileName))
            {

                //commandClassCollection cCC = new commandClassCollection();
                Stream TestFileStream = File.OpenRead(FileName);
                //BinaryFormatter deserializer = new BinaryFormatter();
                //SoapFormatter deserializer = new SoapFormatter();
                var deserializer = new XmlSerializer(typeof(List<ContextCommands>));

                try
                {
                    //cCC = (commandClassCollection)deserializer.Deserialize(TestFileStream);
                    //commands = cCC.Collection;
                    commands = (List<ContextCommands>)deserializer.Deserialize(TestFileStream);
                }
                catch (Exception e1)
                {
                    MessageBox.Show(e1.ToString());
                }
                TestFileStream.Close();

                if (commands != null)
                    foreach (ContextCommands comm in commands)
                    {
                        MessageBox.Show(comm.commTSN + " " + comm.commTST);
                        contextMenuStrip1.Items.Add(comm.commTSN);
                        contextMenuStrip1.Items[contextMenuStrip1.Items.Count - 1].Text = comm.commTST;
                        contextMenuStrip1.Items[contextMenuStrip1.Items.Count - 1].Click += new System.EventHandler(addCommandButton1);
                    }
            }
        }
    }
}