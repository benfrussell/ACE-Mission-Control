using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ACE_Mission_Control.Core.Models
{
    public class OnboardComputerController : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public static volatile PrivateKeyFile PrivateKey;
        private static Timer connectTimer;

        private static bool _keyOpen;
        public static bool KeyOpen
        {
            get { return _keyOpen; }
            set
            {
                if (value == _keyOpen)
                    return;
                _keyOpen = value;
                NotifyStaticPropertyChanged();
            }
        }

        public OnboardComputerController()
        {
            KeyOpen = false;
        }
        // Static methods

        public static void StartTryingConnections()
        {
            connectTimer = new Timer(5000);
            // Hook up the Elapsed event for the timer.
            connectTimer.Elapsed += StartConnectionEvent;
            connectTimer.AutoReset = true;
            connectTimer.Enabled = true;
        }

        public static void StartConnectionEvent(Object source, ElapsedEventArgs e)
        {
            foreach (Drone d in DroneController.Drones)
            {
                string result;
                bool success = d.OBCClient.TryConnect(out result);
                if (!success)
                    continue;
            }
        }

        public static string OpenPrivateKey(string passphrase)
        {
            string error = null;
            try
            {
                if (passphrase == null || passphrase.Length == 0)
                {
                    error = "Passphrase cannot be empty.";
                }
                else
                {
                    string app_dir = Environment.CurrentDirectory.Substring(0, Environment.CurrentDirectory.LastIndexOf("\\"));
                    PrivateKey = new PrivateKeyFile(app_dir + @"\Scripts\key_gen\keys\id_rsa", passphrase);
                    KeyOpen = true;
                    StartTryingConnections();
                }
            }
            catch (InvalidOperationException)
            {
                error = "Incorrect passphrase.";
            }
            catch (FileNotFoundException)
            {
                error = "Could not find the key file. Make sure to generate a key and configure the onboard computer.";
            }
            catch (UnauthorizedAccessException)
            {
                error = "Access to the key file was denied. Ensure the application and user have permission to read the file.";
            }
            finally
            {
                UpdateAllStatus();
            }
            return error;
        }

        private static void UpdateAllStatus()
        {
            foreach (Drone d in DroneController.Drones)
                d.OBCClient.UpdateStatus();
        }

        public static async Task<string> OpenPrivateKeyAsync(string passphrase)
        {
            return await Task.Run(() => OpenPrivateKey(passphrase));
        }

        public static void GenerateSSHKeyFiles(string password)
        {
            string key_gen_dir = Environment.CurrentDirectory.Substring(0, Environment.CurrentDirectory.LastIndexOf("\\")) + @"\Scripts\key_gen\";

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "\"" + key_gen_dir + "key_gen.exe\"";
            startInfo.Arguments = String.Format(". {0}", password);
            startInfo.UseShellExecute = true;
            process.StartInfo = startInfo;
            process.Start();
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
