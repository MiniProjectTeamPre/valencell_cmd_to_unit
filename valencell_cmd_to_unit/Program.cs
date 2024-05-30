using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using USBClassLibrary;
using System.Management;

namespace valencell_cmd_to_unit {
    class Program {
        private static cmdAr cmd = new cmdAr();
        private static SerialPort mySerialPort = new SerialPort();
        static private List<USBClassLibrary.USBClass.DeviceProperties> ListOfUSBDeviceProperties;
        private static bool debug = true;
        private static bool flag_discom = false;
        private static System.Threading.Timer close_program;
        private static string head = "1";
        private static string command = "START";
        private static bool flag_uid = true;
        private static string[] vlc_step = { "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8", "s9", "s10", "s11", "s12", "s13" };
        static void Main(string[] args) {
            while (true) {
                try { head = File.ReadAllText("../../config/head.txt"); break; } catch { Thread.Sleep(50); }
            }
            File.Delete("../../config/head.txt");
            File.WriteAllText("call_exe_tric.txt", "");
            //File.WriteAllText("../../config/valencell_cmd_to_unit_step_" + head + ".txt", "START");
            //File.WriteAllText("../../config/test_head_" + head + "_debug.txt", "True");
            int timeout_close = 80000;
            string port_name = "COM0";
            try { port_name = File.ReadAllText("../../config/vlc_port_" + head + ".txt"); } catch { }
            try { command = File.ReadAllText("../../config/valencell_cmd_to_unit_step_" + head + ".txt"); } catch { }
            try { debug = Convert.ToBoolean(File.ReadAllText("../../config/test_head_" + head + "_debug.txt")); } catch { }
            close_program = new System.Threading.Timer(TimerCallback, null, 0, timeout_close);
            mySerialPort.PortName = port_name;
            mySerialPort.BaudRate = 9600;
            mySerialPort.DataBits = 8;
            mySerialPort.StopBits = StopBits.One;
            mySerialPort.Parity = Parity.None;
            mySerialPort.Handshake = Handshake.None;
            mySerialPort.RtsEnable = true;
            mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            Console.WriteLine("Head = " + head);
            Console.WriteLine("Port Name = " + mySerialPort.PortName);
            Console.WriteLine("Baud Rate = " + mySerialPort.BaudRate);
            Console.WriteLine("Command = " + command);
            //if (debug) Console.ReadLine();

            Stopwatch time_open = new Stopwatch();
            aaaa:
            time_open.Restart();
            while (time_open.ElapsedMilliseconds < 5000) {
                try {
                    mySerialPort.Open();
                    time_open.Stop();
                    break;
                } catch { Thread.Sleep(250); }
                try { mySerialPort.Close(); } catch { }
            }
            if (time_open.IsRunning) {
                if (!flag_discom) {
                    try { mySerialPort.Close(); } catch { }
                    flag_wait_discom = true;
                    discom("disable", port_name);
                    discom("enable", port_name);
                    flag_discom = true;
                    flag_wait_discom = false;
                    goto aaaa;
                }
                try { mySerialPort.Close(); } catch { }
                File.WriteAllText("test_head_" + head + "_result.txt", "can not open port\r\nFAIL");
                Environment.Exit(0);
                Application.Exit();
                return;
            }
            bool flag_checksum = false;
            File.Delete("vlc_cmd_to_unit_stop_" + head + ".txt");

            if (command == "START") {
                int restart = 0;
                start_label:
                bool flag_ack = false;
                string start_err = "";
                for (int hkj = 0; hkj < 6; hkj++) { //for นี้ เอาไว้ดัก ack เฉยๆ
                    Console.WriteLine("send: " + command);
                    byte[] data_new = { };
                    try { mySerialPort.DiscardInBuffer(); } catch {
                        start_err = "Send Err";
                        Thread.Sleep(100);
                        continue;
                    }
                    send_cmd(cmd.Start, data_new);
                    Stopwatch time_ack = new Stopwatch();
                    time_ack.Restart();
                    while (time_ack.ElapsedMilliseconds < 2000) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_ack.Stop();
                        break;
                    }
                    if (time_ack.IsRunning) {
                        start_err = "Not Ack";
                        if (!flag_discom) {
                            try { mySerialPort.Close(); } catch { }
                            flag_wait_discom = true;
                            discom("disable", port_name);
                            discom("enable", port_name);
                            flag_discom = true;
                            flag_wait_discom = false;
                            goto aaaa;
                        }
                        continue;
                    }
                    if (rx_hex.Count == 0) continue;
                    if (rx_hex[0] != 0x94) continue;
                    rx_hex.Clear();
                    flag_ack = true;
                    break;
                }
                if (!flag_ack) {
                    Console.WriteLine("Not Ack");
                    File.WriteAllText("test_head_" + head + "_result.txt", start_err + "\r\nPASS");
                    cmd_stop();
                    return;
                }

                int[] rx_hex_sup = new int[0];
                bool flag_uid = false;
                for (int hkj = 0; hkj < 3; hkj++) { //for นี้ เอาไว้ดัก UID
                    Stopwatch time_uid = new Stopwatch();
                    if (hkj != 0) {
                        byte[] data_new = { };
                        send_cmd(cmd.UID, data_new);
                    }
                    time_uid.Restart();
                    while (time_uid.ElapsedMilliseconds < 4000) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_uid.Stop();
                        break;
                    }
                    rx_hex_sup = rx_hex.ToArray();
                    rx_hex.Clear();
                    if (time_uid.IsRunning) continue;
                    if (rx_hex_sup.Length != 12) continue;
                    if (rx_hex_sup[0] != cmd.Head) continue;
                    if (rx_hex_sup[1] != cmd.UID) continue;
                    if (rx_hex_sup[2] != (rx_hex_sup.Length - 3)) continue;
                    flag_uid = true;
                    break;
                }
                string sss = "";
                for (int i = 3; i < rx_hex_sup.Length - 1; i++) {
                    sss += rx_hex_sup[i].ToString("X2");
                }
                if (!flag_uid) {
                    Console.WriteLine("UID Err : " + sss);
                    File.WriteAllText("test_head_" + head + "_result.txt", "Read UID Err:" + sss + "\r\nPASS");
                    cmd_stop();
                    return;
                } else {
                    Console.WriteLine("UID : " + sss);
                    if (restart < 1)
                        File.WriteAllText("test_head_" + head + "_result.txt", sss + "00000000000000000000000000000000\r\nPASS");
                }

                Stopwatch time_send_data = new Stopwatch();
                time_send_data.Restart();
                Stopwatch time_result = new Stopwatch();
                time_result.Restart();
                int time_out = 40000;
                for (int hkj = 0; hkj < 5; hkj++) { //for นี้ เอาไว้ดัก result
                    if (hkj != 0) {
                        byte[] data_new = { };
                        send_cmd(cmd.Result, data_new);
                    }
                    while (time_result.ElapsedMilliseconds < time_out) {
                        try {
                            string bvnb = File.ReadAllText("vlc_cmd_to_unit_stop_" + head + ".txt");
                            File.Delete("vlc_cmd_to_unit_stop_" + head + ".txt");
                            File.WriteAllText("test_head_" + head + "_result.txt", "*ERROR* STOP" + sss + "\r\nPASS");
                            cmd_stop();
                            return;
                        } catch { }
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_result.Stop();
                        break;
                    }
                    if (time_result.IsRunning) { time_out = 1000; continue; }
                    if (rx_hex.Count != 90) continue;
                    if (rx_hex[0] != cmd.Head) continue;
                    if (rx_hex[1] != cmd.Result) continue;
                    if (rx_hex[2] != (rx_hex.Count - 3)) continue;
                    if (rx_hex[rx_hex.Count - 1] != check_sum(rx_hex)) continue;
                    break;
                }
                string[] bbb = new string[rx_hex.Count];
                string result_hex = "";
                for (int i = 0; i < rx_hex.Count; i++) {
                    bbb[i] = rx_hex[i].ToString("X2");
                    result_hex += bbb[i];
                }
                File.WriteAllText("test_head_" + head + "_result_sup.txt", result_hex);
                if (rx_hex.Count != 90) {
                    File.WriteAllText("test_head_" + head + "_result.txt", "*ERROR* 90 byte" + "\r\nPASS");
                    cmd_stop();
                    return;
                }
                if (rx_hex[0] == 0x00 && rx_hex[1] == 0x00 && rx_hex[2] == 0x00 && rx_hex[3] == 0x00 && rx_hex[4] == 0x00) {
                    Console.WriteLine("Result = 00000000000, num = 90 byte");
                    if (restart < 1) { Thread.Sleep(5000); restart++; goto start_label; } else {
                        //File.WriteAllText("test_head_" + head + "_result.txt", "Arduino Restart" + "\r\nFAIL");
                        //cmd_stop();
                        //return;

                        //debug = true;
                        //Console.ReadLine();
                        //goto start_label;
                        File.WriteAllText("test_head_" + head + "_result.txt", "*ERROR* Read UID:" + "\r\nPASS");
                        cmd_stop();
                        return;
                    }
                }
                string result = "RESULT ";
                try {
                    result += rx_hex[3] + " ";
                    string vbn = "";
                    for (int i = 7; i > 3; i--) vbn += rx_hex[i].ToString("X2");
                    float ccvb = FromHexString(vbn);
                    result += ccvb + " ";
                    result += vlc_step[0] + "|" + sss + "00000000000000000000000000000000|1";
                    result += "|" + vlc_step[1] + "|" + bbb[18] + bbb[19] + "." + bbb[20] + bbb[21] + "|" + rx_hex[22];
                    result += "|" + vlc_step[2] + "|" + bbb[24] + bbb[25] + "|" + rx_hex[28];
                    vbn = "";
                    for (int i = 33; i > 29; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[3] + "|" + ccvb + "|" + rx_hex[34];
                    vbn = "";
                    for (int i = 39; i > 35; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[4] + "|" + ccvb + "|" + rx_hex[40];
                    vbn = "";
                    for (int i = 45; i > 41; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[5] + "|" + ccvb + "|" + rx_hex[46];
                    vbn = "";
                    for (int i = 51; i > 47; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[6] + "|" + ccvb + "|" + rx_hex[52];
                    bool current_fail = false;
                    vbn = "";
                    for (int i = 57; i > 53; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[7] + "|" + ccvb + "|" + rx_hex[58];
                    vbn = "";
                    for (int i = 63; i > 59; i--) vbn += rx_hex[i].ToString("X2");
                    ccvb = FromHexString(vbn);
                    result += "|" + vlc_step[8] + "|" + ccvb + "|" + rx_hex[64];
                    result += "|" + vlc_step[9] + "|" + bbb[66] + "|" + rx_hex[70];
                    result += "|" + vlc_step[10] + "|" + rx_hex[72] + "|" + rx_hex[76];
                    result += "|" + vlc_step[11] + "|" + rx_hex[78] + "|" + rx_hex[82];
                    result += "|" + vlc_step[12] + "|" + rx_hex[84] + "|" + rx_hex[88];
                } catch {
                    File.WriteAllText("test_head_" + head + "_result.txt", "*ERROR* Convert Result" + sss + "\r\nPASS");
                    cmd_stop();
                    return;
                }
                while(time_send_data.ElapsedMilliseconds < 15000) { Thread.Sleep(100); }
                Console.WriteLine("Result : " + result);
                File.WriteAllText("test_head_" + head + "_result.txt", result + "\r\nPASS");
            } else if (command.Contains("LOAD")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 2, 2);
                if (XX == "LL") data[0] = 0x01;
                else if (XX == "LM") data[0] = 0x02;
                else if (XX == "LH") data[0] = 0x03;
                bool flag_load = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Load, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command.Contains("DRIVE")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 1, 1);
                if (XX == "L") data[0] = 0x01;
                else if (XX == "H") data[0] = 0x02;
                bool flag_load = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Drive, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command.Contains("POWER")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 2, 2);
                if (XX == "ON") data[0] = 0x01;
                bool flag_load = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Power, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command.Contains("SETLED")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 2, 2);
                switch (XX) {
                    case "L1": data[0] = 0x01; break;
                    case "L2": data[0] = 0x02; break;
                    case "L3": data[0] = 0x03; break;
                    case "12": data[0] = 0x04; break;
                }
                bool flag_load = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Set_Led, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command.Contains("GETLED")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 2, 2);
                switch (XX) {
                    case "L1": data[0] = 0x01; break;
                    case "L2": data[0] = 0x02; break;
                    case "L3": data[0] = 0x03; break;
                }
                bool flag_load = false;
                float value_led = 0;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Get_Led, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;

                    if (rx_hex.Count != 9) continue;
                    if (rx_hex[0] != cmd.Head) continue;
                    if (rx_hex[1] != cmd.Get_Led) continue;
                    if (rx_hex[2] != (rx_hex.Count - 3)) continue;
                    string sss = "";
                    for (int i = 7; i > 3; i--) sss += rx_hex[i].ToString("X2");
                    value_led = FromHexString(sss);
                    if (rx_hex[rx_hex.Count - 1] != check_sum(rx_hex)) continue;

                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", value_led.ToString() + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command.Contains("VDD")) {
                byte[] data = { 0x00 };
                string XX = command.Substring(command.Length - 1, 1);
                switch (XX) {
                    case "L": data[0] = 0x01; break;
                    case "H": data[0] = 0x02; break;
                }
                bool flag_load = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.debug, data);
                    Stopwatch time_load = new Stopwatch();
                    time_load.Restart();
                    while (time_load.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_load.Stop();
                        break;
                    }
                    if (time_load.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_load = true;
                    break;
                }
                if (flag_load) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else if (command == "STOP") {
                byte[] data = { };
                bool flag_stop = false;
                for (int hkj = 0; hkj < 3; hkj++) {
                    Console.WriteLine("send: " + command);
                    send_cmd(cmd.Stop, data);
                    Stopwatch time_stop = new Stopwatch();
                    time_stop.Restart();
                    while (time_stop.ElapsedMilliseconds < 1500) {
                        if (flag_data != true) { Thread.Sleep(100); continue; }
                        flag_data = false;
                        time_stop.Stop();
                        break;
                    }
                    if (time_stop.IsRunning) continue;
                    if (rx_hex.Count != 1) continue;
                    if (rx_hex[0] != cmd.Ack) continue;
                    flag_stop = true;
                    break;
                }
                if (flag_stop) File.WriteAllText("test_head_" + head + "_result.txt", "Send OK" + "\r\nPASS");
                else File.WriteAllText("test_head_" + head + "_result.txt", "Send Err" + "\r\nFAIL");
            } else
                File.WriteAllText("test_head_" + head + "_result.txt", "cmd err" + "\r\nFAIL");
            if (debug) Console.ReadLine();
            Environment.Exit(0);
            Application.Exit();
        }

        private static string byte2string(byte[] data) {
            string ss = "";
            foreach (byte x in data) ss += x.ToString("X2") + " ";
            return ss;
        }
        private static byte check_sum(List<int> dd) {
            byte ss = 0;
            for (int i = 0; i < dd.Count - 1; i++) ss += Convert.ToByte(dd[i]);
            return ss;
        }
        private static byte[] StringToByteArray(string hex) {
            if (hex.Length == 1) hex = "0" + hex;
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        private static string ToHexString(float f) {
            byte[] bytes = BitConverter.GetBytes(f);
            int i = BitConverter.ToInt32(bytes, 0);
            return i.ToString("X8");
        }
        private static float FromHexString(string s) {
            int i = Convert.ToInt32(s, 16);
            byte[] bytes = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(bytes, 0);
        }
        private static void send_cmd(byte cmds, byte[] data_send) {
            List<byte> cmdData = new List<byte>();
            cmdData.Add(cmd.Head);
            cmdData.Add(cmds);
            byte[] intBytes = BitConverter.GetBytes(data_send.Length);
            intBytes[0] += 1;
            cmdData.Add(intBytes[0]);
            foreach (byte xx in data_send) cmdData.Add(xx);
            byte checkSum = 0;
            foreach (byte xx in cmdData) checkSum += xx;
            cmdData.Add(checkSum);
            byte[] sends = cmdData.ToArray();
            rx_hex.Clear();
            try {
                mySerialPort.DiscardInBuffer();
                mySerialPort.DiscardOutBuffer();
            } catch { }
            flag_data = false;
            try { mySerialPort.Write(sends, 0, sends.Length); } catch {
                Console.WriteLine("<< err");
                return;
            }
            Console.WriteLine(">> " + byte2string(sends));
        }
        private static string check_sum(string data) {
            CRC32 crc = new CRC32();
            byte[] bytes = Encoding.ASCII.GetBytes(data);
            byte[] bytes_ = crc.ComputeHash(bytes);
            string checksum_hex = ByteArrayToString(bytes_);
            string checksum_int = Int64.Parse(checksum_hex, System.Globalization.NumberStyles.HexNumber).ToString();
            return checksum_int;
        }
        private static void fail_uid_digit(int kji = 0) {
            Console.WriteLine("data not " + kji + " digit");
            command = "UID?";
            Console.WriteLine("send: " + command);
            byte[] data = { };
            send_cmd(cmd.UID, data);
        }
        private static void fail_uid_checksum() {
            Console.WriteLine("checksum not same");
            command = "UID?";
            Console.WriteLine("send: " + command);
            byte[] data = { };
            send_cmd(cmd.UID, data);
        }
        private static void fail_result_checksum() {
            Console.WriteLine("checksum not same");
            command = "RESULT?";
            Console.WriteLine("send: " + command);
            byte[] data = { };
            send_cmd(cmd.Result, data);
        }
        private static void cmd_stop() {
            Thread.Sleep(500);
            string cmd_stop = "STOP";
            Console.WriteLine("send: " + cmd_stop);
            byte[] data = { };
            send_cmd(cmd.Stop, data);
            if (debug) Console.ReadLine();
            Environment.Exit(0);
            Application.Exit();
        }

        static List<int> rx_hex = new List<int>();
        static bool flag_data = false;
        private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e) {
            Thread.Sleep(50);
            int length = 0;
            try {
                while (true) {
                    mySerialPort = (SerialPort)sender;
                    try { length = mySerialPort.BytesToRead; } catch { return; }
                    if (length == 0) return;
                    int buf = 0;
                    for (int i = 0; i < length; i++) {
                        buf = mySerialPort.ReadByte();
                        rx_hex.Add(buf);
                    }
                    Thread.Sleep(50);
                    if (mySerialPort.BytesToRead != 0) continue;
                    break;
                }
            } catch { return; }
            try {
                mySerialPort.DiscardInBuffer();
                mySerialPort.DiscardOutBuffer();

            } catch { }
            string sss = "";
            foreach (int xx in rx_hex) sss += xx.ToString("X2") + " ";
            Console.WriteLine("<< " + sss);
            flag_data = true;
        }

        private static bool flag_close = false;
        private static void TimerCallback(Object o) {
            if (!flag_close) { flag_close = true; return; }
            if (debug || flag_wait_discom) return;
            File.WriteAllText("test_head_" + head + "_result.txt", "timeout main\r\nFAIL");
            Environment.Exit(0);
            Application.Exit();
        }
        private static bool flag_wait_discom = false;
        private static void discom(string cmd, string comport) {//enable disable//
            ManagementObjectSearcher objOSDetails2 = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'");
            ManagementObjectCollection osDetailsCollection2 = objOSDetails2.Get();
            foreach (ManagementObject usblist in osDetailsCollection2) {
                string arrport = usblist.GetPropertyValue("NAME").ToString();
                if (arrport.Contains(comport)) {
                    Process devManViewProc = new Process();
                    devManViewProc.StartInfo.FileName = "DevManView.exe";
                    devManViewProc.StartInfo.Arguments = "/" + cmd + " \"" + arrport + "\"";
                    devManViewProc.Start();
                    devManViewProc.WaitForExit();
                }
            }
        }
        public static string ByteArrayToString(byte[] ba) {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
    }

    public class CRC32 {
        private readonly uint[] ChecksumTable;
        private readonly uint Polynomial = 0xEDB88320;

        public CRC32() {
            ChecksumTable = new uint[0x100];

            for (uint index = 0; index < 0x100; ++index) {
                uint item = index;
                for (int bit = 0; bit < 8; ++bit)
                    item = ((item & 1) != 0) ? (Polynomial ^ (item >> 1)) : (item >> 1);
                ChecksumTable[index] = item;
            }
        }

        public byte[] ComputeHash(Stream stream) {
            uint result = 0xFFFFFFFF;

            int current;
            while ((current = stream.ReadByte()) != -1)
                result = ChecksumTable[(result & 0xFF) ^ (byte)current] ^ (result >> 8);

            byte[] hash = BitConverter.GetBytes(~result);
            Array.Reverse(hash);
            return hash;
        }

        public byte[] ComputeHash(byte[] data) {
            using (MemoryStream stream = new MemoryStream(data))
                return ComputeHash(stream);
        }
    }
    public class cmdAr {
        public byte Start = 0x01;
        public byte Stop = 0x02;
        public byte TesterID = 0x03;
        public byte Result = 0x04;
        public byte UID = 0x05;
        public byte SpecVersion = 0x06;
        public byte Calibrate = 0x08;
        public byte Current = 0x09;
        public byte Load = 0x0A;
        public byte Drive = 0x0B;
        public byte SetFactor = 0x0C;
        public byte Update = 0x0D;
        public byte Power = 0x0F;
        public byte Head = 0x49;
        public byte Ack = 0x94;
        public byte Set_Led = 0x10;
        public byte Get_Led = 0x11;
        public byte debug = 0x12;
    }
}
