using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Timers;


namespace KimClockManager
{
    class Program
    {
        private static SerialPort sPort;

        // typed version
        //private static string clockProgram = "200 A2.E9.CA.AD.60.00.8D.FB.00.AD.61.00.8D.FA.00.AD.62.00.8D.F9.00.8E.63.00.8C.64.00.20.1F.1F.AE.63.00.AC.64.00.E0.00.D0.DA.F8.38.A9.00.6D.62.00.8D.62.00.D8.C9.60.D0.C9.A9.54.20.A0.1E.F8.38.A9.00.8D.62.00.6D.61.00.8D.61.00.D8.C9.60.D0.B2.F8.38.A9.00.8D.62.00.8D.61.00.6D.60.00.8D.60.00.D8.C9.13.D0.9D.A9.01.8D.60.00.C9.01.F0.94.20.5C.18.";
        // punchtape version
        private static string clockProgram = "L;180200A2E9CAAD60008DFB00AD61008DFA00AD62008DF9008E63000B1F~;1802188C6400201F1FAE6300AC6400E000D0DAF838A9006D62008D0960~;1802306200D8C960D0C9A95420A01EF838A9008D62006D61008D610AA5~;18024800D8C960D0B2F838A9008D62008D61006D60008D6000D8C90AF6~;18026013D09DA9018D6000C901F094205C1800002A81BD860100000862~;0000050005~";
        private static Timer tock;
        private static int shortPause = 400;
        private static int longPause = 15000;
        private static int resetSet = 10;
        private static int resetWait = 100;
        private static int minuteTimer = 0;
        private static int hourTimer = 0;
        private static string minuteBuffer = "";
        private static List<char> dataBuffer;
        private static int STATE = 0;
        private static int NEXTSTATE = STATE;
        private static Random rnd1 = new Random();

        static void Main(string[] args)
        {

            dataBuffer = new List<char>();

            Console.WriteLine("opening port: " + args[0]);
            sPort = new SerialPort(args[0], 1200, Parity.None, 8, StopBits.Two);
            sPort.Handshake = Handshake.None;
            sPort.Open();

            sPort.DtrEnable = false;
            sPort.ReadExisting();

            tock = new Timer(shortPause);
            tock.AutoReset = false;
            tock.Elapsed += new ElapsedEventHandler(tock_Elapsed);
            tock.Start();


            // run the timer loop until RETRUN is pressed
            Console.ReadLine();
        }

        private static void tock_Elapsed(Object source, ElapsedEventArgs e)
        {
            Console.Write("\n");
            // process input buffer
            string inData = sPort.ReadExisting();

            // do work
            gotoState(STATE, inData);


            inData = inData.Replace("\n", "<lf>");
            inData = inData.Replace("\r", "<cr>");
            inData = inData.Replace("\t", "<tab>");
            inData = inData.Replace((char)8, '^');
            inData = inData.Replace(" ", "_");
            Console.Write(" IN: " + inData + "\n");

            // send output buffer
            sendData();

            tock.Start();
        }

        private static void STATE0(string inData)
        {
            // RESET
            Console.WriteLine("RESET");
            sPort.DtrEnable = true;
            System.Threading.Thread.Sleep(resetSet + rnd1.Next(10));
            sPort.DtrEnable = false;
            System.Threading.Thread.Sleep(resetWait);

            // send RETURN
            //toBuffer((char)8);
            toBuffer("\r");
            
            //toBuffer((char)8);

            NEXTSTATE = 1;
        }
        private static void STATE1(string inData)
        {
            // check input for KIM
            if (inData.Contains("KIM"))
            {
                NEXTSTATE = 2;
            }
            else
            {
                NEXTSTATE = 0;
            }

        }
        private static void STATE2(string inData)
        {
            if (dataBuffer.Count == 0)
            {
                // send vectors
                toBuffer("17FA 00.1C.17FE 00.1C.");

                // send program
                toBuffer(clockProgram);

                NEXTSTATE = 3;
            }
        }
        private static void STATE3(string inData)
        {
            // wait for program to finish sending
            if (dataBuffer.Count == 0)
            {
                // set time
                sendTime();

                // send 200 SPACE
                toBuffer("200 ");

                // send G
                toBuffer("G");

                NEXTSTATE = 4;
            }
        }
        private static void STATE4(string inData)
        {
            // slow down timer since we don't have to check as often
            tock.Interval = longPause;
            // check input for heartbeat (S) every minute
            // if heartbreak missed then go to STATE0
            minuteTimer++;
            minuteBuffer += inData;
            if (minuteTimer > 4)
            {
                if (minuteBuffer.Length == 0) 
                {
                    tock.Interval = shortPause; 
                    NEXTSTATE = 0;
                }
                Console.Write("minBuffer: " + minuteBuffer + "\t hour: " + hourTimer + "\t"); 
                minuteBuffer = "";
                minuteTimer = 0;
                hourTimer += 1;
            }
            Console.Write("min: " + minuteTimer + "\t");

            // reset every 24 hours
            if (hourTimer > 1152)
            {
                tock.Interval = shortPause;
                NEXTSTATE = 0;
            }
        }
        private static void sendTime()
        {
            // set time
            int hour = DateTime.Now.Hour;
            if (hour > 12) { hour -= 12; }

            int min = DateTime.Now.Minute;
            int sec = DateTime.Now.Second;

            toBuffer("60 " + hour + "." + min + "." + sec + ".");
        }
        private static void gotoState(int s, string inData)
        {
            Console.Write("STATE: " + s + "\t");
            switch (s)
            {
                case 0:
                    STATE0(inData);
                    break;
                case 1:
                    STATE1(inData);
                    break;
                case 2:
                    STATE2(inData);
                    break;
                case 3:
                    STATE3(inData);
                    break;
                case 4:
                    STATE4(inData);
                    break;
            }
        }
        private static void sendData()
        {
            Console.Write("\t\tOUT: ");
            if (dataBuffer.Count > 0)
            {
                char[] c = { dataBuffer[0] };

                if (c[0] == '~') 
                {
                    dataBuffer.RemoveAt(0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, (char)0);
                    dataBuffer.Insert(0, '\n');
                    dataBuffer.Insert(0, '\r');
                    Console.Write("~"); 
                } else {

                    if (c[0] == '\r') { Console.Write("<cr>"); } 
                    else if (c[0] == (char)8) { Console.Write("<bs>"); } 
                    else { Console.Write(c); }

                    sPort.Write(c, 0, 1);
                    dataBuffer.RemoveAt(0);   
                }
            }
            if (dataBuffer.Count == 0)
            {
                STATE = NEXTSTATE;
            }
            Console.Write("\n");
        }
        private static void toBuffer(string str)
        {
            foreach (char c in str)
            {
                dataBuffer.Add(c);
            }
        }
        private static void toBuffer(char c)
        {
            dataBuffer.Add(c);
        }
    }
}

// STATE0 = send RETURN
// STATE1 = wait for KIM responce, if no response go to STATE0
// 17FA SPACE
// 00.1C.
// 17FE SPACE
// 00.1C.
// 200 SPACE
// send program  ##.##.##....
// 200 SPACE
// G - starts the program
// listen for heartbeat
// if heartbeat missed RESET, then go to top

