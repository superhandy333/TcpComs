/* 
 * Datei:     tcpcoms.cs
 * Version:   25.05.2018
 * Besitzer:  Mathias Rentsch (rentsch@online.de)
 * Lizenz:    GPL
 *
 * Die Anwendung und die Quelltextdateien sind freie Software und stehen unter der
 * GNU General Public License. Der Originaltext dieser Lizenz kann eingesehen werden
 * unter http://www.gnu.org/licenses/gpl.html.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Linq;
using System.Net.Sockets;
using System.Diagnostics;
using Tools.Database;
using System.Collections.Concurrent;
using System.Threading;
using System.Net.NetworkInformation;

namespace Tools
{
    public class TcpComs
    {
        private IPAddress localAddr = IPAddress.Any;
        private const int localPort = 13000;
        private const int maxLocalPorts = 5;
        private const int threadPause = 1000;
        private TcpListener listener = null;
        public ConcurrentBag<TcpClient> Clients;
        private int listenerPort = 0;
        private bool runned;
        
        public event EventHandler<DataEventArgs> ReceiveData;

        public TcpComs()
        {
            lgg("TcpComs: Start");
            Clients = new ConcurrentBag<TcpClient>();
            runned = true;
            listenerStart();
            new Thread(listenerRun).Start(); 
            new Thread(init1).Start();

        }
        public int ListenerPort
        {
            get
            {
                return listenerPort;
            }
        }
        public void Senden(byte[] bytes)
        {
            if (bytes != null)
            {
                if (bytes.Length > 0)
                {
                    ThreadPool.QueueUserWorkItem(new WaitCallback((o)=>
                    {
                        sendenIntern(bytes);
                    }));
                }
                else
                {
                    lgg("Senden: Die zu sendende Byte-Anzahl muss mindestens 1 sein.");
                }
            }
            else
            {
                lgg("Senden: Byte-Array nicht initialisiert (null).");
            }
        }
        private void sendenIntern(byte[] bytes)
        {
            try
            {
                lgg("SendenIntern");
                foreach (TcpClient client in Clients)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            NetworkStream stream = client.GetStream();
                            //stream.WriteByte(anzahl);
                            stream.Write(bytes, 0, bytes.Length);
                            lgg("SendenIntern: " + bytes.Length.ToString() + " Bytes erfolgreich gesendet an: " + client.Client.RemoteEndPoint.ToString());
                        }
                        else
                        {
                            lgg("SendenIntern: Senden fail, da client nicht connected");
                        }

                    }
                    catch (Exception)
                    {
                        lgg("SendenIntern: Exception in client");
                    }
                }
            }
            catch
            {
                lgg("SendenIntern: Exception");
            }
        }
        private void init1()
        {
            byte[] bytes = new byte[4];
            foreach (NetworkInterface netzwerk in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netzwerk.OperationalStatus == OperationalStatus.Up)
                {
                    UnicastIPAddressInformationCollection adresses = netzwerk.GetIPProperties().UnicastAddresses;

                    foreach (IPAddressInformation info in adresses)
                    {
                        if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            bytes = info.Address.GetAddressBytes();
                            lgg("Netzwerkadapter: " + info.Address + " " + netzwerk.Name);
                            init2(bytes[0], bytes[1], bytes[2]);
                        }
                    }
                }
            }
        }
        private void init2(int sec1, int sec2, int sec3)
        {
            if (1 == 1
                & sec1 != 129  // aus bestimmten Gründen  ;-)
                & sec1 != 0
                //& sec1 != 127
                )
            {
                int sec4max =
                   (sec1 == 127 &
                     sec2 == 0 &
                     sec3 == 0
                    )
                ? 1 : 254;   // Ausnahme bei localhost (sec1=127)

                for (int sec4 = 1; sec4 <= sec4max; sec4++)
                {
                    pingE(getAddress(sec1, sec2, sec3, sec4));
                    //lgg(sec1.ToString() + ":" + sec2.ToString() + ":" + sec3.ToString() + ":" + sec4.ToString());
                }
            }
        }
        private void pingE(IPAddress address)  // Ping mit Einzelthreads
        {
            if (address != null)
            {
                // Diese Threads nicht aus dem Threadpool nehmen, da sonst kurz an 
                // init der TcpComs-Instanz die Rest-Ping-Threads die ersten Senden-Threads
                // blockieren
                new Thread(() =>
                {
                    try
                    {
                        Ping ping = new Ping();
                        PingReply reply = ping.Send(address);
                        //lgg(address.ToString() + ": Ping " + reply.Status);
                        if (reply.Status == IPStatus.Success)
                        {
                            lgg("Ping Success: " + address.ToString());
                            connect(address);
                        }
                        else
                        {
                            lgg("Ping Fail: " + address.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        lgg("Ping Exception: " + address.ToString() + " " + ex.Message);
                    }
                }).Start();
            }
            else
            {
                lgg("Ping Fail: IPAddress is NULL");
            }
        }
        /*
        private void pingP(IPAddress address)  // Ping mit Threads aus Threadpool
        {
            if (address != null)
            {

                if (ThreadPool.QueueUserWorkItem(new WaitCallback((o) =>
                {
                    try
                    {
                        lgg("PingP: " + address.ToString());
                        Ping ping = new Ping();
                        PingReply reply = ping.Send(address);
                        //lgg(address.ToString() + ": Ping " + reply.Status);
                        if (reply.Status == IPStatus.Success)
                        {
                            lgg("Ping: " + address.ToString() + " Success");
                            connect(address);
                        }
                    }
                    catch
                    {
                        lgg("Ping: " + address.ToString() + " Exception");
                    }
                }
            )) == false)
                {
                    lgg("PingP: Thread konnte nicht erstellt werden.");
                }
                else
                {
                    lgg("PingP: Thread erstellt "+address.ToString());
                }

            }
            else
            {
                lgg("IPAddress NULL in ping(IPAddress)");
            }
        }
        */
        private void connect(IPAddress address)
        {
            try
            {
                if (address != null)
                {
                    for (int i = 0; i < maxLocalPorts; i++)
                    {
                        IPEndPoint remoteEndpoint = new IPEndPoint(address, localPort + i);
                        if (address.Equals(IPAddress.Loopback))   // localhost
                        {
                            if (localPort + i == listenerPort)
                            {
                                lgg("Connect abgewiesen, da eigener ListenerEndPoint auf localhost. " + remoteEndpoint.ToString());
                            }
                            else
                            {
                                connectInternThread(remoteEndpoint);
                            }
                        }
                        else
                        {
                            if (isOwnListenerAddress(address))
                            {
                                lgg("Connect abgewiesen, da eigene Adresse (Verbindungen zum eigenen Rechner werden über localhost abgewickelt) " + address.ToString());
                            }
                            else
                            {
                                connectInternThread(remoteEndpoint);
                            }
                        }
                    }
                }
                else
                {
                    lgg("Connect Fail: IPAddress is NULL in connect(IPAddress)");
                }
            }
            catch (Exception ex)
            {
                lgg("Connect Exception: " + address.ToString() + " in connect(IPAddress) " + ex.Message);
            }

        }
        private void connectInternThread(IPEndPoint remoteEndpoint)
        {
            new Thread(() =>
            {
                if (connect(remoteEndpoint))  // localPort ist auch der Zielport der Gegenseite
                {
                    lgg("Connect Success: " + remoteEndpoint.ToString());
                }
                else
                {
                    lgg("Connect Fail: " + remoteEndpoint.ToString());
                }
            }).Start();
        }
        private bool connect(IPEndPoint remoteEndpoint)
        {
            bool ret = false;
            try
            {
                if (remoteEndpoint != null)
                {
                    TcpClient client = new TcpClient();
                    client.Connect(remoteEndpoint);

                    if (!isExist(client))
                    {
                        lgg("Connect: " + remoteEndpoint.ToString() + " Success: Neuer Client " + client.GetHashCode());
                        ret = true;
                        Clients.Add(client);
                        lgg("Connect: Client " + client.GetHashCode().ToString() + " wurde clients hinzugefügt.");
                        new Thread(receive).Start(client);
                    }
                    else
                    {
                        client.Dispose();
                        lgg("Connect Success: " + remoteEndpoint.ToString() + " Verworfen, da Verbindung schon vorhanden.");
                    }
                }
            }
            catch (Exception ex)
            {
                lgg("Connect Exception: " + remoteEndpoint.ToString() + " in connect(IPEndPoint) " + ex.Message);
            }
            return ret;
        }
        private bool isOwnListenerAddress(IPAddress address)
        {
            // Adresse darf nicht an die eigene Adresse sein
            // Verbindungen auf dem eigenen Rechner werden stattdessen über localhost abgewickelt
            bool b = false;
            if (address != null)
            {
                foreach (NetworkInterface netzwerk in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netzwerk.OperationalStatus == OperationalStatus.Up)
                    {
                        UnicastIPAddressInformationCollection adresses = netzwerk.GetIPProperties().UnicastAddresses;
                        foreach (IPAddressInformation info in adresses)
                        {
                            if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                if (address.Equals(info.Address))
                                {
                                    b = true;
                                }
                            }
                        }
                    }
                }
            }
            return b;
        }
        /*private bool isOwnListenerEndPoint(IPEndPoint endpoint)
        {
            // EndPoint darf nicht an der eigenen Adresse mit dem eigenen MyListenerPort liegen
            bool b = false;
            if (endpoint != null)
            {
                if (endpoint.Address != null)
                {
                    foreach (NetworkInterface netzwerk in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (netzwerk.OperationalStatus == OperationalStatus.Up)
                        {
                            UnicastIPAddressInformationCollection adresses = netzwerk.GetIPProperties().UnicastAddresses;
                            foreach (IPAddressInformation info in adresses)
                            {
                                if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    if (endpoint.Address.Equals(info.Address))
                                    {
                                        if (endpoint.Port == listenerPort)
                                        {
                                            b = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return b;
        }
        */
        private IPAddress getAddress(int sec1, int sec2, int sec3, int sec4)
        {
            IPAddress address = null;
            if ((sec1 > -1 & sec1 < 256) & (sec2 > -1 & sec2 < 256) & (sec3 > -1 & sec3 < 256) & (sec4 > -1 & sec4 < 256))
            {
                byte[] bytes = new byte[4];
                bytes[0] = (byte)sec1;
                bytes[1] = (byte)sec2;
                bytes[2] = (byte)sec3;
                bytes[3] = (byte)sec4;
                address = new IPAddress(bytes);
            }
            return address;
        }
        private void listenerStart()
        {
            try
            {
                lgg("Listener: Start...");

                for (int i = 0; i < maxLocalPorts; i++)
                {
                    try
                    {
                        listener = new TcpListener(localAddr, localPort + i);
                        listener.Start();       //throw new Exception();
                        lgg("Listener gestartet. Lokale Adresse: " + localAddr.ToString() + ":" + (localPort + i).ToString());
                        listenerPort = localPort + i;
                        i = 10; // raus
                    }
                    catch (Exception)
                    {
                        lgg("ListenerStart: Exception: Listener konnte am lokalen Port: " + localPort.ToString() + " (alle Adapter) nicht gestartet werden (Port besetzt?)");
                        listener = null;
                    }
                }
            }
            catch
            {
                lgg("ListenerStart: Exception");
            }
        }
        private void listenerRun()
        {
            try
            {
                if (listener != null)
                {
                    lgg("ListenerRun: Waiting for a connection... ");
                    while (runned)
                    {
                        if (listener.Pending())
                        {
                            TcpClient client = listener.AcceptTcpClient();

                            if (!isExist(client))
                            {
                                lgg("ListenerRun: " + client.Client.LocalEndPoint.ToString() + " connected mit " + client.Client.RemoteEndPoint.ToString() + " Neuer Empfangs-Client " + client.GetHashCode());

                                Clients.Add(client);
                                lgg("ListenerRun: Client " + client.GetHashCode() + " wurde clients hinzugefügt.");

                                new Thread(receive).Start(client);
                            }
                            else
                            {
                                lgg("ListenerRun: " + client.Client.LocalEndPoint.ToString() + " connected mit " + client.Client.RemoteEndPoint.ToString() + " Client verworfen, da Verbindung schon vorhanden");
                                client.Dispose();
                            }
                        }
                        Thread.Sleep(threadPause); //Console.WriteLine("Stop Listener"); Console.ReadLine();
                    }
                }
                else
                {
                    lgg("ListenerRun: Kein Listener vorhanden (null)");
                }
            }
            catch
            {
                lgg("ListenerRun: Exception");
            }
        }
        private void receive(object obj)
        {
            TcpClient client;
            try
            {
                if (obj != null)
                {
                    client = (TcpClient)obj;
                    Byte[] bytes = new Byte[3000];
                    NetworkStream stream = client.GetStream(); //Console.WriteLine("Stop Receiver"); Console.ReadLine();
                    while (runned)
                    {
                        if (stream.DataAvailable)
                        {
                            int i = stream.Read(bytes, 0, bytes.Length);
                            lgg("Receiver: " + i.ToString() + " Bytes empfangen");
                            byte[] sendbytes = new byte[i];
                            Array.Copy(bytes, sendbytes, i);
                            if (ReceiveData != null) ReceiveData(this, new DataEventArgs(client.Client.RemoteEndPoint,sendbytes)); // Wichtig ist hier, dass die Daten geklont werden
                        }
                        Thread.Sleep(threadPause);
                    }
                }
            }
            catch (Exception ex)
            {
                lgg("Receive: Exception " + ex.Message);
            }

        }
        private bool isExist(TcpClient client)
        {
            bool b = false;
            if (client != null)
            {
                foreach (TcpClient c in Clients)
                {
                    if (
                        (client.Client.LocalEndPoint.ToString() == c.Client.LocalEndPoint.ToString() &
                          client.Client.RemoteEndPoint.ToString() == c.Client.RemoteEndPoint.ToString()) |

                        (client.Client.LocalEndPoint.ToString() == c.Client.RemoteEndPoint.ToString() &
                          client.Client.RemoteEndPoint.ToString() == c.Client.LocalEndPoint.ToString())
                        )
                    {
                        b = true;
                        lgg("isExist: Ausschluss");
                    }
                }
            }
            return b;
        }
        public void Close()
        {
            lgg("Close");
            foreach (TcpClient client in Clients)
            {
                client.Close();
            }
            runned = false;
        }
        public bool IsRunned
        {
            get
            {
                return runned;
            }
        }
        private void lgg(string text)
        {
            Console.WriteLine(text);
            //s.w(text);   // Privater Logging-Mechanismus
        }
    }
}

