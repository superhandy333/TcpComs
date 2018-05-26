using System;
using System.Collections.Concurrent;
using System.Windows.Forms;

namespace Tools
{
    public class SampleForm : Form
    {
        TcpComs tcp;
        ConcurrentQueue<byte> byteQueue;
        ListBox listbox1;
        FlowLayoutPanel panel1;
        Button button1;
        Button button2;
        Random random;
        System.Windows.Forms.Timer timer;

        public SampleForm()
        {
            InitializeComponent();
            byteQueue = new ConcurrentQueue<byte>();
            random = new Random(DateTime.Now.Millisecond);
            
            tcp = new TcpComs();
            tcp.ReceiveData += Tcp_ReceiveData;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 500;
            timer.Tick += Timer_Tick;
            timer.Enabled = true;
        }

        void Tcp_ReceiveData(object sender, DataEventArgs e)
        {
            foreach (byte b in e.Data)
            {
                byteQueue.Enqueue(b);
            }
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            byte b;
            while (byteQueue.TryDequeue(out b))
            {
                lgg(b.ToString());
            }
        }

        void button1_Click(object sender, EventArgs e)
        {
            const int maxBytes = 50;
            byte[] bytes = new byte[maxBytes];
            random.NextBytes(bytes);
            tcp.Senden(bytes);
            lgg(maxBytes.ToString()+" Bytes gesendet");
        }

        void button2_Click(object sender, EventArgs e)
        {
            tcp.Close();
            Close();
        }

        void lgg(string text)
        {
            listbox1.Items.Add(text);
            listbox1.SelectedIndex = listbox1.Items.Count - 1;
        }

        void InitializeComponent()
        {
            panel1 = new FlowLayoutPanel();
            panel1.FlowDirection = FlowDirection.LeftToRight;
            panel1.Height = 30;
            panel1.Dock = DockStyle.Top;
            Controls.Add(panel1);

            button1 = new Button();
            button1.Text = "Senden";
            button1.Click += button1_Click;
            panel1.Controls.Add(button1);

            button2 = new Button();
            button2.Text = "Beenden";
            button2.Click += button2_Click;
            panel1.Controls.Add(button2);

            listbox1 = new ListBox();
            listbox1.Dock = DockStyle.Fill;
            Controls.Add(listbox1);
        }
    }
}
