using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Net;
using aUpdater;
using INI;
using AndroidCtrl;
using AndroidCtrl.ADB;
using AndroidCtrl.AAPT;
using AndroidCtrl.Tools;
using AndroidCtrl.Signer;
using AndroidCtrl.Fastboot;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HTC_One_M8_Toolkit_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show(
                    "There seems to be another instance of the toolkit running. Please make sure it is not running in the background.",
                    "Another Instance is running", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            InitializeComponent();
        }
        //Device names
        private string fullDeviceName = "M8 (HTC One M8)";
        private string codeDeviceName = "m8";
        //Download int
        private int retryLvl = 0;
        //TWRP options
        private string twrpVersion;
        private string pTWRPMD5;
        private string pTWRPFileName;
        //SuperSU options
        private string suVersion;
        private string suType;
        private bool suManInstall;
        private string pSuFileName;
        //Webclients for downloads
        private WebClient _twrpClient;
        private WebClient _suClient;
        private WebClient _driverClient;
        //Lists from ini files
        List<String> twrpListStrLineElements;
        List<String> suListStrLineElements;
        //App driectory
        public static string appPath = System.AppDomain.CurrentDomain.BaseDirectory;

        public void CheckandDeploy()
        {
            if (ADB.IntegrityCheck() == false)
            {
                Deploy.ADB();
            }
            if (Fastboot.IntegrityCheck() == false)
            {
                Deploy.Fastboot();
            }
            // Check if ADB is running
            if (ADB.IsStarted)
            {
                // Stop ADB
                ADB.Stop();

                // Force Stop ADB
                ADB.Stop(true);
            }
            else
            {
                // Start ADB
                ADB.Start();
            }
        }

        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                {
                    using (var stream = client.OpenRead("http://www.mircosoft.com"))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public void downloadCachedFiles()
        {
            bool inetR = CheckForInternetConnection();
            if (inetR == true)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile("https://s.basketbuild.com/dl/devs?dl=squabbi/toolkits/TWRPBuildList.ini", "./Data/.cached/TWRPBuildList.ini");
                        client.DownloadFile("https://s.basketbuild.com/dl/devs?dl=squabbi/superSU/SuBuildList.ini", "./Data/.cached/SuBuildList.ini");
                        client.Dispose();
                    }
                }
                catch
                {
                    MessageBox.Show(string.Join("An active internet connection was not found! You will only be able to flash your own images and zips untill you restart the toolkit with an internet connection.",
                        "If the problem persists, check your firewall to allow the toolkit as an exeption. Cached files will be used instead and may be out of date."), "No internet Connection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(string.Join("An active internet connection was not found! You will only be able to flash your own images and zips untill you restart the toolkit with an internet connection.",
                    "If the problem persists, check your firewall to allow the toolkit as an exeption. Cached files will be used instead and may be out of date."), "No internet Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void getBuildLists()
        {
            //TWRP Lists
            IniFileName iniTWRP = new IniFileName("./Data/.cached/TWRPBuildList.ini");
            string[] twrpSelectionValues = iniTWRP.GetEntryNames(fullDeviceName);
            //SuperSU Lists
            IniFileName iniSU = new IniFileName("./Data/.cached/SuBuildList.ini");
            string[] suSecNames = iniSU.GetSectionNames();
            string[] suSelectionValues = iniSU.GetEntryNames(suSecNames[0]);                 
            //Foreach entry make a combobox item
            foreach (string twrpSecVal in twrpSelectionValues)
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    twrpBuildList.Items.Add(twrpSecVal);
                });
            }
            foreach (string suSecVal in suSelectionValues)
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    supersuBuildList.Items.Add(suSecVal);
                });
            }
        }

        public void Add(List<string> msg)
        {
            foreach (string tmp in msg)
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    console.Document.Blocks.Add(new Paragraph(new Run(tmp.Replace("(bootloader) ", ""))));
                });
            }
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                console.ScrollToEnd();
            });
        }

        public void cAppend(string message)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                console.AppendText(string.Format("\n{0} :: {1}", DateTime.Now, message));
                console.ScrollToEnd();
            });
        }

        private void CheckFileSystem()
        {
            try
            {
                string[] neededDirectories = new string[] { "Data/", "Data/Logs", "Data/Downloads", "Data/.cached",
                "Data/Downloads/TWRP", "Data/Downloads/SU"};
                foreach (string dir in neededDirectories)
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
            }
            catch (Exception ex)
            {
                //Declare variable for AppData folder
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                MessageBox.Show(
                    "An error has occured. A this point the program should be able to read and write in this directory. Check the the output log in the toolkit's 'appdata'" +
                    " folder by pressing OK. \n\n You can try re-running the toolkit as an Administrator.",
                    "Folder Creation Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                string fileDateTime = DateTime.Now.ToString("MMddyyyy") + "_" + DateTime.Now.ToString("HHmmss");
                var file = new StreamWriter(appData + "/SquabbiXDA/StartUp Error " + fileDateTime + ".txt");
                file.WriteLine(ex);
                file.Close();
                Process.Start(appData + "/SquabbiXDA/");
            }
        }

        public void CheckDeviceState(List<DataModelDevicesItem> devices)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                // Here we refresh our combobox
                SetDeviceList();
            });
        }

        private void SetDeviceList()
        {
            string active = String.Empty;
            if (deviceselector.Items.Count != 0)
            {
                active = ((DataModelDevicesItem)deviceselector.SelectedItem).Serial;
            }

            App.Current.Dispatcher.Invoke((Action)delegate
            {
                // Here we refresh our combobox
                deviceselector.Items.Clear();
            });

            // This will get the currently connected ADB devices
            List<DataModelDevicesItem> adbDevices = ADB.Devices();

            // This will get the currently connected Fastboot devices
            List<DataModelDevicesItem> fastbootDevices = Fastboot.Devices();

            foreach (DataModelDevicesItem device in adbDevices)
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    // here goes the add command ;)
                    deviceselector.Items.Add(device);
                });
            }
            foreach (DataModelDevicesItem device in fastbootDevices)
            {
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    deviceselector.Items.Add(device);
                });
            }
            if (deviceselector.Items.Count != 0)
            {
                int i = 0;
                bool empty = true;
                foreach (DataModelDevicesItem device in deviceselector.Items)
                {
                    if (device.Serial == active)
                    {
                        empty = false;
                        deviceselector.SelectedIndex = i;
                        break;
                    }
                    i++;
                }
                if (empty)
                {

                    // This calls will select the BASE class if we have no connected devices
                    ADB.SelectDevice();
                    Fastboot.SelectDevice();

                    App.Current.Dispatcher.Invoke((Action)delegate
                    {
                        deviceselector.SelectedIndex = 0;
                    });
                }
            }
        }

        private void DeviceDetectionService()
        {
            ADB.Start();

            // Here we initiate the BASE Fastboot instance
            Fastboot.Instance();

            //This will starte a thread which checks every 10 sec for connected devices and call the given callback
            if (Fastboot.ConnectionMonitor.Start())
            {
                //Here we define our callback function which will be raised if a device connects or disconnects
                Fastboot.ConnectionMonitor.Callback += ConnectionMonitorCallback;

                // Here we check if ADB is running and initiate the BASE ADB instance (IsStarted() will everytime check if the BASE ADB class exists, if not it will create it)
                if (ADB.IsStarted)
                {
                    //Here we check for connected devices
                    SetDeviceList();

                    //This will starte a thread which checks every 10 sec for connected devices and call the given callback
                    if (ADB.ConnectionMonitor.Start())
                    {
                        //Here we define our callback function which will be raised if a device connects or disconnects
                        ADB.ConnectionMonitor.Callback += ConnectionMonitorCallback;
                    }
                }
            }
        }

        public void ConnectionMonitorCallback(object sender, ConnectionMonitorArgs e)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                // Do what u want with the "List<DataModelDevicesItem> e.Devices"
                // The "sender" is a "string" and returns "adb" or "fastboot"
                SetDeviceList();

            });
        }

        private void SelectDeviceInstance(object sender, SelectionChangedEventArgs e)
        {
            if (deviceselector.Items.Count != 0)
            {
                DataModelDevicesItem device = (DataModelDevicesItem)deviceselector.SelectedItem;

                // This will select the given device in the Fastboot and ADB class
                Fastboot.SelectDevice(device.Serial);
                ADB.SelectDevice(device.Serial);
            }
        }

        private async void HTCTokenID()
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
            };

            MessageDialogResult result = await this.ShowMessageAsync("Grabbing Unlock Token ID", "This command will get your device's unique Token ID, then open a text file with your token and further instructions.\n\nAre you ready to continue?",
                        MessageDialogStyle.AffirmativeAndNegative, mySettings);
            if (result == MessageDialogResult.Affirmative)
            {
                IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                if (state == IDDeviceState.DEVICE)
                {
                    var controller = await this.ShowProgressAsync("Waiting For Device...", "");
                    controller.SetTitle("Rebooting into download mode...");
                    await Task.Run(() => ADB.Instance().Reboot(IDBoot.DOWNLOAD));
                    controller.SetTitle("Getting Token ID...");
                    using (StreamWriter sw = File.CreateText("./Data/token.txt"))
                    {
                        List<string> _token = new List<string>();
                        foreach (string line in Fastboot.Instance().OEM.GetIdentifierToken())
                        {
                            GroupCollection groups = Regex.Match(line, @"^\(bootloader\)\s{1,}(?<PART>.*?)$").Groups;
                            string part = groups["PART"].Value;
                            if (String.IsNullOrEmpty(part) == false && Regex.IsMatch(part, @"^<{1,}.*?>{1,}$") == false)
                            {
                                _token.Add(part);
                            }
                        }
                        controller.SetTitle("Collecting token...");
                        string token = String.Join("\n", _token.ToArray());
                        sw.WriteLine(token.ToString());
                        sw.WriteLine(" ");
                        sw.WriteLine("Please copy everything above this line!");
                        sw.WriteLine(" ");
                        sw.WriteLine("Next, sign into your HTC Dev account on the webpage that just opened.");
                        sw.WriteLine("If you do not have an account, create and activate an account with your email, then come back to this link.");
                        sw.WriteLine("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        sw.WriteLine("Then, paste the Token ID you just copied at the bottom of the webpage.");
                        sw.WriteLine("Hit submit, and wait for the email with the unlock file.");
                        sw.WriteLine(" ");
                        sw.WriteLine("Once you have received the unlock file, download it and continue on to the next step, unlocking your bootloader.");
                        sw.WriteLine("This file is saved as token.txt in the Data folder if you need it in the future.");
                        sw.Close();
                    }
                    await controller.CloseAsync();
                    MessageDialogResult result2 = await this.ShowMessageAsync("Successful!", "Would you like to reboot now?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result2 == MessageDialogResult.Affirmative)
                    {
                        var controller2 = await this.ShowProgressAsync("Restarting device...", "\nPlease wait...");
                        await Task.Run(() => Fastboot.Instance().Reboot(IDBoot.REBOOT));
                        await controller2.CloseAsync();
                        Process.Start("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        Process.Start(System.AppDomain.CurrentDomain.BaseDirectory + "/Data/token.txt");
                        await this.ShowMessageAsync("Next Step!", "Once you have received the unlock file from HTC, you can move on to the next step, unlocking your bootloader!",
                    MessageDialogStyle.Affirmative);
                    }
                    else
                    {
                        Process.Start("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        Process.Start(System.AppDomain.CurrentDomain.BaseDirectory + "/Data/token.txt");
                        await this.ShowMessageAsync("Next Step!", "Once you have received the unlock file from HTC, you can move on to the next step, unlocking your bootloader!",
                    MessageDialogStyle.Affirmative);
                    }
                }
                else if (state == IDDeviceState.FASTBOOT)
                {
                    var controller = await this.ShowProgressAsync("Waiting For Device...", "");
                    controller.SetTitle("Getting Token ID...");
                    using (StreamWriter sw = File.CreateText("./Data/token.txt"))
                    {
                        List<string> _token = new List<string>();
                        foreach (string line in Fastboot.Instance().OEM.GetIdentifierToken())
                        {
                            GroupCollection groups = Regex.Match(line, @"^\(bootloader\)\s{1,}(?<PART>.*?)$").Groups;
                            string part = groups["PART"].Value;
                            if (String.IsNullOrEmpty(part) == false && Regex.IsMatch(part, @"^<{1,}.*?>{1,}$") == false)
                            {
                                _token.Add(part);
                            }
                        }
                        controller.SetTitle("Collecting token...");
                        string token = String.Join("\n", _token.ToArray());
                        sw.WriteLine(token.ToString());
                        sw.WriteLine(" ");
                        sw.WriteLine("Please copy everything above this line!");
                        sw.WriteLine(" ");
                        sw.WriteLine("Next, sign into your HTC Dev account on the webpage that just opened.");
                        sw.WriteLine("If you do not have an account, create and activate an account with your email, then come back to this link.");
                        sw.WriteLine("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        sw.WriteLine("Then, paste the Token ID you just copied at the bottom of the webpage.");
                        sw.WriteLine("Hit submit, and wait for the email with the unlock file.");
                        sw.WriteLine(" ");
                        sw.WriteLine("Once you have received the unlock file, download it and continue on to the next step, unlocking your bootloader.");
                        sw.WriteLine("This file is saved as token.txt in the Data folder if you need it in the future.");
                        sw.Close();
                    }
                    await controller.CloseAsync();
                    MessageDialogResult result2 = await this.ShowMessageAsync("Successful!", "Would you like to reboot now?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result2 == MessageDialogResult.Affirmative)
                    {
                        var controller2 = await this.ShowProgressAsync("Restarting device...", "\nPlease wait...");
                        await Task.Run(() => Fastboot.Instance().Reboot(IDBoot.REBOOT));
                        await controller2.CloseAsync();
                        Process.Start("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        Process.Start(System.AppDomain.CurrentDomain.BaseDirectory + "/Data/token.txt");
                        await this.ShowMessageAsync("Next Step!", "Once you have received the unlock file from HTC, you can move on to the next step, unlocking your bootloader!",
                    MessageDialogStyle.Affirmative);
                    }
                    else
                    {
                        Process.Start("http://www.htcdev.com/bootloader/unlock-instructions/page-3");
                        Process.Start(System.AppDomain.CurrentDomain.BaseDirectory + "/Data/token.txt");
                        await this.ShowMessageAsync("Next Step!", "Once you have received the unlock file from HTC, you can move on to the next step, unlocking your bootloader!",
                    MessageDialogStyle.Affirmative);
                    }
                }
                else
                {
                    await this.ShowMessageAsync("No Device Found!", "Please ensure that USB Debugging is enabled, your device is plugged in correctly, and you correctly installed the ADB and HTC Drivers." +
                        "\n\nIf this is a persistent issue, please reply in the XDA thread for resolutions.",
                                        MessageDialogStyle.Affirmative);
                }
            }
        }

        private async void HTCDeviceUnlock()
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
            };

            MessageDialogResult result = await this.ShowMessageAsync("Unlocking the Bootloader", "This will unlock your bootloader and completely wipe the data your device.\n\nYou must have downloaded the unlock_code.bin file from HTC (via your e-mail).\n\nThis will VOID your warranty!\n\nAre you ready to continue?",
                    MessageDialogStyle.AffirmativeAndNegative, mySettings);
            if (result == MessageDialogResult.Affirmative)
            {
                OpenFileDialog ofd = new OpenFileDialog { CheckFileExists = true, Filter = "HTC Unlock File (*.bin)|*.bin", Multiselect = false };
                ofd.ShowDialog();
                if (File.Exists(ofd.FileName))
                {
                    IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                    if (state == IDDeviceState.DEVICE)
                    {
                        var controller = await this.ShowProgressAsync("Waiting For Device...", "");
                        controller.SetTitle("Rebooting into Download Mode...");
                        await Task.Run(() => ADB.Instance().Reboot(IDBoot.DOWNLOAD));
                        controller.SetTitle("Flashing Unlock File... Hold on tight!");
                        controller.SetMessage("Your device will now display a screen asking you about unlocking. Please read it carefully. Use your volume buttons to choose Yes, then press the power button confirm the unlock.");
                        await Task.Run(() => Fastboot.Instance().Flash(IDDevicePartition.UNLOCKTOKEN, ofd.FileName.ToString()));
                        await controller.CloseAsync();
                        await this.ShowMessageAsync("Done!", "Your bootloader is now unlocked (congrats) ;), and your device will reboot. You can now move on to the next step, flashing a recovery. Please ensure you enable USB Debugging after your device finishes rebooting!", MessageDialogStyle.Affirmative);
                    }
                    else if (state == IDDeviceState.FASTBOOT)
                    {
                        var controller = await this.ShowProgressAsync("Waiting For Device...", "");
                        controller.SetTitle("Flashing Unlock File... Hold on tight!");
                        controller.SetMessage("Your device will now display a screen asking you about unlocking. Please read it carefully. Use your volume buttons to choose Yes, then press the power button confirm the unlock.");
                        await Task.Run(() => Fastboot.Instance().Flash(IDDevicePartition.UNLOCKTOKEN, ofd.FileName.ToString()));
                        await controller.CloseAsync();
                        await this.ShowMessageAsync("Done!", "Your bootloader is now unlocked (congrats) ;), and your device will reboot. You can now move on to the next step, flashing a recovery. Please ensure you enable USB Debugging after your device finishes rebooting!", MessageDialogStyle.Affirmative);
                    }
                    else
                    {
                        await this.ShowMessageAsync("No Device Found!", "Please ensure that USB Debugging is enabled, your device is plugged in correctly, and you correctly installed the ADB Drivers. If this is a persistent issue, please reply in the XDA thread for solutions.",
                                            MessageDialogStyle.Affirmative);
                    }
                }
            }
        }

        private void btnLinkHtcDev_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.htcdev.com/register/");
        }

        private void btnGetUnlockCode_Click(object sender, RoutedEventArgs e)
        {
            HTCTokenID();
        }

        private void btnUnlockBootloader_Click(object sender, RoutedEventArgs e)
        {
            HTCDeviceUnlock();
        }

        private void btnOpenTokenCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(System.AppDomain.CurrentDomain.BaseDirectory + "/Data/token.txt");
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error has occurred!\nLook below for more infomation. Most likely you don't have a token.txt file!\n\n" + ex, "Your TOKEN cannot be opened", MessageBoxButton.OK, MessageBoxImage.Error);
                string fileDateTime = DateTime.Now.ToString("MMddyyyy") + "_" + DateTime.Now.ToString("HHmmss");
                var file = new StreamWriter("./Data/Logs/" + fileDateTime + ".txt");
                file.WriteLine(ex);
                file.Close();
            }
        }

        private async void flashTWRP()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var controllerTWRPflash = await this.ShowProgressAsync("Flashing Recovery...", "");
            controllerTWRPflash.SetIndeterminate();

            cAppend("Flashing TWRP...");
            await Task.Run(() => Fastboot.Instance().Flash(IDDevicePartition.RECOVERY, "./Data/Downloads/TWRP/" + pTWRPFileName));
            cAppend("Done flashing TWRP.\n");
            await controllerTWRPflash.CloseAsync();

            var result = await this.ShowMessageAsync("Flash Successful!", "Would you like to reboot now?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
            if (result == MessageDialogResult.Affirmative)
            {
                cAppend("Rebooting device...");
                await Task.Run(() => Fastboot.Instance().Reboot(IDBoot.REBOOT));
            }
        }

        private async void flashSuperSU()
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var manSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "OK",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
            if (state == IDDeviceState.DEVICE)
            {
                var controllerSUflash = await this.ShowProgressAsync(string.Format("Pushing {0}...", pSuFileName), "Flashing OTA via Sideload");
                controllerSUflash.SetIndeterminate();

                cAppend(string.Format("Pushing {0} to your device...", pSuFileName));
                await Task.Run(() => ADB.WaitForDevice());
                await Task.Run(() => ADB.Instance().Push(Path.Combine("./Data/Downloads/SU", pSuFileName), "/sdcard/"));
                cAppend("Rebooting to recovery...");
                controllerSUflash.SetTitle("Rebooting to recovery...");
                await Task.Run(() => ADB.Instance().Reboot(IDBoot.RECOVERY));
                if (suManInstall == false)
                {
                    cAppend("Waiting for device...");
                    //await Task.Run(() => ADB.WaitForDevice());
                    controllerSUflash.SetTitle(string.Format("Flashing {0}...", pSuFileName));
                    cAppend(string.Format("Flashing {0}...", pSuFileName));
                    await Task.Run(() => ADB.Instance().ShellCmd(string.Format("twrp install /sdcard/{0}", pSuFileName)));
                    cAppend("Done!");
                    await controllerSUflash.CloseAsync();
                    var result = await this.ShowMessageAsync("Flash Completed!",
                    string.Format("Did you see the flashing screen on your phone? If not, you may need to manually install {0} from /sdcard. \n\nPressing Yes will reboot your phone.")
                    , MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        cAppend("Rebooting...");
                        //await Task.Run(() => ADB)
                    }
                }
                else
                {
                    await this.ShowMessageAsync("Rebooting into recovery", "Locate the SuperSU.zip in /sdcard/ and flash it!", MessageDialogStyle.Affirmative, manSettings);
                }
            }
            else if (state == IDDeviceState.FASTBOOT)
            {
                cAppend("You'll need to be in the recovery...");
                cAppend("Please reboot your phone into recovery and try again.\n");
            }
            else if (state == IDDeviceState.RECOVERY)
            {
                var controllerSUflash = await this.ShowProgressAsync(string.Format("Pushing {0}...", pSuFileName), "Flashing OTA via Sideload");
                controllerSUflash.SetIndeterminate();

                cAppend(string.Format("Pushing {0} to your device...", pSuFileName));
                //await Task.Run(() => ADB.WaitForDevice());
                await Task.Run(() => ADB.Instance().PushPullUTF8.Push(Path.Combine("./Data/Downloads/SU", pSuFileName), "/sdcard/"));
                if (suManInstall == false)
                {
                    cAppend("Waiting for device...");
                    await Task.Run(() => ADB.WaitForDevice());
                    controllerSUflash.SetTitle(string.Format("Flashing {0}...", pSuFileName));
                    cAppend(string.Format("Flashing {0}...", pSuFileName));
                    await Task.Run(() => ADB.Instance().ShellCmd(string.Format("twrp install /sdcard/{0}", pSuFileName)));
                    cAppend("Done!");
                    await controllerSUflash.CloseAsync();
                    var result = await this.ShowMessageAsync("Flash Completed!",
                    string.Format("Did you see the flashing screen on your phone? If not, you may need to manually install {0} from /sdcard. \n\nPressing Yes will reboot your phone.")
                    , MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        cAppend("Rebooting...");
                        //await Task.Run(() => ADB)
                    }
                }
                else
                {
                    await this.ShowMessageAsync("Rebooting into recovery", "Locate the SuperSU.zip in /sdcard/ and flash it!", MessageDialogStyle.Affirmative, manSettings);
                }
                await controllerSUflash.CloseAsync();
            }
            else
            {
                cAppend("Your device was not detected by the Connection Monitor, you may have not installed the drivers correctly.");

                var result = await this.ShowMessageAsync("No device detected",
                    "You can press Yes if you are sure your device is in the correct state and connected. Otherwise, press No to try again later.",
                            MessageDialogStyle.AffirmativeAndNegative, mySettings);
                if (result == MessageDialogResult.Affirmative)
                {
                    if (suManInstall == false)
                    {
                        var controllerSUflash = await this.ShowProgressAsync("Waiting for device...", "");
                        cAppend("Waiting for device...");
                        await Task.Run(() => ADB.WaitForDevice());
                        controllerSUflash.SetTitle(string.Format("Flashing {0}...", pSuFileName));
                        cAppend(string.Format("Flashing {0}...", pSuFileName));
                        await Task.Run(() => ADB.Instance().ShellCmd(string.Format("twrp install /sdcard/{0}", pSuFileName)));
                        cAppend("Done!");
                        await controllerSUflash.CloseAsync();
                        var result2 = await this.ShowMessageAsync("Flash Completed!",
                        string.Format("Did you see the flashing screen on your phone? If not, you may need to manually install {0} from /sdcard. \n\nPressing Yes will reboot your phone.")
                        , MessageDialogStyle.AffirmativeAndNegative, mySettings);
                        if (result2 == MessageDialogResult.Affirmative)
                        {
                            cAppend("Rebooting...");
                            //await Task.Run(() => ADB)
                        }
                    }
                    else
                    {
                        await this.ShowMessageAsync("Rebooting into recovery", "Locate the SuperSU.zip in /sdcard/ and flash it!", MessageDialogStyle.Affirmative, manSettings);
                    }
                }
            }
        }

        private void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                statusProgress.Value = int.Parse(Math.Truncate(percentage).ToString());
            });
        }

        private async void twrpClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var md5mismatchSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "Re-download",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var md5matchSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "Later",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            //Check if it was cancled
            if (e.Cancelled)
            {
                //Reset retrys
                retryLvl = 0;
                _twrpClient.Dispose();
                //Set progress to 0
                statusProgress.Value = 0;
                cAppend("Download cancelled!");
                cAppend("Cleaning up...\n");
                //Delete attemped file download
                if (File.Exists(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName)))
                {
                    cAppend(string.Format("Deleting {0}...", pTWRPFileName));
                    cAppend("Ready for next download.");
                    File.Delete(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName));
                }
            }
            //In case of error
            else if (e.Error != null)
            {
                // We have an error! Retry a few times, then abort.
                retryLvl++;
                if (retryLvl < 3)
                {
                    cAppend(string.Format("Failed.. {0} retry...", retryLvl.ToString()));
                    cAppend(string.Format("Error: {0}\n", e.Error.Message));
                    btnFlashTwrp_Click(new object(), new RoutedEventArgs());
                }
                else
                {
                    retryLvl = 0;
                    cAppend(string.Format("Failed after 3 retries... Error: {0}", e.Error.Message));
                }
            }
            //No error
            else if (!e.Cancelled)
            {
                //Reset retrys
                retryLvl = 0;
                //Clean up webclient
                _twrpClient.Dispose();
                statusProgress.Value = 0;
                //start flashing
                statusProgress.IsIndeterminate = true;
                bool lResult = await Task.Run(() => checkTWRPMd5());
                if (lResult == true)
                {
                    var matchResult = await this.ShowMessageAsync("MD5 Match", string.Format("The MD5s have returned as a match. Are you ready to flash {0}?", pTWRPFileName), MessageDialogStyle.AffirmativeAndNegative, md5matchSettings);
                    if (matchResult == MessageDialogResult.Affirmative)
                    {
                        IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                        if (state == IDDeviceState.DEVICE || state == IDDeviceState.RECOVERY)
                        {
                            var controllerRebootingRecoveryFlash = await this.ShowProgressAsync("Waiting for device...", "");
                            controllerRebootingRecoveryFlash.SetIndeterminate();
                            cAppend("Waiting for device...");
                            await Task.Run(() => ADB.WaitForDevice());
                            cAppend("Rebooting to the bootloader.");
                            await Task.Run(() => ADB.Instance().Reboot(IDBoot.BOOTLOADER));
                            await controllerRebootingRecoveryFlash.CloseAsync();
                            flashTWRP();
                        }
                        else if (state == IDDeviceState.FASTBOOT)
                        {
                            flashTWRP();
                        }
                        else
                        {
                            cAppend("Your device is in the wrong state. Please put your device in the bootloader.\n");
                        }
                    }
                }
                else
                {
                    var result = await this.ShowMessageAsync("MD5 Check Failed", "The MD5s are not the same! This means the file is corrupt and probably wasn't downloaded properly. Would you like to re-download (recommended)?",
                                MessageDialogStyle.AffirmativeAndNegative, md5mismatchSettings);
                    if (result == MessageDialogResult.Negative)
                    {
                        File.Delete(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName));
                        btnFlashTwrp_Click(new object(), new RoutedEventArgs());
                    }
                    else
                    {
                        IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                        if (state == IDDeviceState.DEVICE || state == IDDeviceState.RECOVERY)
                        {
                            var controllerRebootingRecoveryFlash = await this.ShowProgressAsync("Waiting for device...", "");
                            controllerRebootingRecoveryFlash.SetIndeterminate();
                            cAppend("Waiting for device...");
                            await Task.Run(() => ADB.WaitForDevice());
                            cAppend("Rebooting to the bootloader.");
                            await Task.Run(() => ADB.Instance().Reboot(IDBoot.BOOTLOADER));
                            await controllerRebootingRecoveryFlash.CloseAsync();
                            flashTWRP();
                        }
                        else if (state == IDDeviceState.FASTBOOT)
                        {
                            flashTWRP();
                        }
                        else
                        {
                            cAppend("Your device is in the wrong state. Please put your device in the bootloader.\n");
                        }
                    }
                }
                statusProgress.IsIndeterminate = false;
            }
        }

        private bool checkTWRPMd5()
        {
            //Check MD5
            cAppend("Starting MD5 check...\n");
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName)))
                {
                    string compHashDownloaded = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                    cAppend(string.Format("Downloaded MD5: {0}", compHashDownloaded));
                    cAppend(string.Format("TWRP's MD5: {0}\n", pTWRPMD5));
                    if (compHashDownloaded == pTWRPMD5)
                    {
                        //MD5 are the same
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        private void suClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            //Check if it was cancled
            if (e.Cancelled)
            {
                //Clean up
                _suClient.Dispose();
                //Set retries back to 0
                retryLvl = 0;
                //Set progress to 0
                statusProgress.Value = 0;
                cAppend("Download cancelled!");
                cAppend("Cleaning up...\n");
                //Delete attemped file download
                if (File.Exists(Path.Combine("./Data/Downloads/SU", pSuFileName)))
                {
                    cAppend(string.Format("Deleting {0}...", pSuFileName));
                    cAppend("Ready for next download...");
                    File.Delete(Path.Combine("./Data/Downloads/SU", pSuFileName));
                }
            }
            //In case of error
            else if (e.Error != null)
            {
                // We have an error! Retry a few times, then abort.
                retryLvl++;
                if (retryLvl < 3)
                {
                    //Delete attemped file download
                    if (File.Exists(Path.Combine("./Data/Downloads/SU", pSuFileName)))
                    {
                        cAppend(string.Format("Deleting {0}...", pSuFileName));
                        cAppend("Ready for next download...");
                        File.Delete(Path.Combine("./Data/Downloads/SU", pSuFileName));
                    }
                    cAppend(string.Format("Failed.. {0} retry...", retryLvl.ToString()));
                    cAppend(string.Format("Error: {0}\n", e.Error.Message));
                    pushSU_Click(new object(), new RoutedEventArgs());
                }
                else
                {
                    retryLvl = 0;
                    cAppend(string.Format("Failed after 3 retries... Error: {0}", e.Error.Message));
                }
            }
            //No error
            else if (!e.Cancelled)
            {
                //Clean Up
                _suClient.Dispose();
                //Reset retrys
                retryLvl = 0;
                //Start flashing
                flashSuperSU();
            }
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var controllerLoad = await this.ShowProgressAsync("Setting up toolkit...", "");
            controllerLoad.SetIndeterminate();

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion;
            //Set Version Label
            menuVersion.Header = "Version: " + version;
            this.Title = string.Format("Squabbi's HTC One {0} Toolkit - v{1}", codeDeviceName, version);
            cAppend("Set version.");
            controllerLoad.SetMessage("Set version.");
            //Checks and Creates folders
            cAppend("Checking folder structure.");
            controllerLoad.SetMessage("Checking folder structure.");
            await Task.Run(() => CheckFileSystem());
            //Download Stock list and add entries to combobox
            if (!File.Exists("./debug"))
            {
                cAppend("Downloading file lists.");
                controllerLoad.SetMessage("Downloading file lists.");
                await Task.Run(() => downloadCachedFiles());
            }
            else
                cAppend("debug file detected. Skipping file list download...");
            cAppend("Populating lists...");
            controllerLoad.SetMessage("Populating lists.");
            await Task.Run(() => getBuildLists());
            cAppend("Deploying ADB & Fastboot.");
            controllerLoad.SetMessage("Deploying ADB & Fastboot.");
            await Task.Run(() => CheckandDeploy());
            cAppend("Starting detection service.");
            controllerLoad.SetMessage("Starting detection service (this may take a while).");
            await Task.Run(() => DeviceDetectionService());
            ////Check for updates
            if (!File.Exists("./debug"))
            {
                cAppend("Checking for updates...\n");
                Updater upd = new Updater();
                upd.UpdateUrl = string.Format("https://s.basketbuild.com/dl/devs?dl=squabbi/{0}/toolkit/UpdateInfo.dat", codeDeviceName);
                upd.CheckForUpdates();
            }
            else
                cAppend("debug file detected. Skipping update.\n");
            await controllerLoad.CloseAsync();
        }

        private async void btnFlashTwrp_Click(object sender, RoutedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var md5mismatchSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "Re-download",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var md5matchSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "Later",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var noImageSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "OK",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            if (twrpBuildList.SelectedIndex == -1)
            {
                cAppend("No recovery image selected.");
                await this.ShowMessageAsync("No TWRP image selected", "Please select an image and try again.", MessageDialogStyle.Affirmative, noImageSettings);
                twrpBuildList.IsDropDownOpen = true;
            }
            if (twrpBuildList.SelectedIndex == 0)
            {
                // Create OpenFileDialog 
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                // Set filter for file extension and default file extension and title
                dlg.Title = "Open file for Custom Recovery";
                dlg.DefaultExt = ".img";
                dlg.Filter = "Custom Recovery (*.img)|*.img";

                // Display OpenFileDialog by calling ShowDialog method 
                Nullable<bool> result = dlg.ShowDialog();

                // Get the selected file name and display in a TextBox 
                if (result == true)
                {
                    // Open document 
                    string filename = dlg.FileName;
                    cAppend(string.Format("Flashing Recovery Image{0}\n", Path.GetFileName(filename)));
                    statusProgress.IsIndeterminate = true;
                    innerGrid.IsEnabled = false;

                    var controllerFlashRecovery = await this.ShowProgressAsync("Flashing Recovery...", filename);
                    controllerFlashRecovery.SetIndeterminate();

                    await Task.Run(() => Fastboot.Instance().Flash(IDDevicePartition.RECOVERY, filename));

                    await controllerFlashRecovery.CloseAsync();
                    innerGrid.IsEnabled = false;
                    statusProgress.IsIndeterminate = false;
                }
            }
            else if (twrpBuildList.SelectedIndex != -1)
            {
                IniFileName iniStock = new IniFileName("./Data/.cached/TWRPBuildList.ini");
                string[] sectionValues = iniStock.GetEntryNames(fullDeviceName);
                string entryValue = iniStock.GetEntryValue(fullDeviceName, twrpBuildList.SelectedValue.ToString()).ToString();

                twrpListStrLineElements = entryValue.Split(';').ToList();

                //Set required variables from ini
                twrpVersion = twrpListStrLineElements[0];
                pTWRPMD5 = twrpListStrLineElements[1];
                pTWRPFileName = string.Format("twrp-{0}-{1}.img", twrpVersion, codeDeviceName);

                if (_suClient != null && _suClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }
                else if (_twrpClient != null && _twrpClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }
                else if (_driverClient != null && _driverClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }
                else
                {
                    //Start checking for existing tgz
                    if (!File.Exists(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName)))
                    {
                        //Start downloading selected tgz
                        cAppend(string.Format("Downloading {0}. Please wait... You can continue to use the program while I download it in the background.", pTWRPFileName));
                        cAppend("You can cancel the download by double clicking the progress bar at any time!\n");
                        //Declare new webclient
                        _twrpClient = new WebClient();
                        //Subscribe to download and completed event handlers
                        _twrpClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                        _twrpClient.DownloadFileCompleted += new AsyncCompletedEventHandler(twrpClient_DownloadFileCompleted);
                        // Starts the download
                        _twrpClient.DownloadFileAsync(new Uri(string.Format("https://s.basketbuild.com/dl/devs?dl=squabbi/{0}/toolkit/{1}", codeDeviceName, pTWRPFileName)), Path.Combine("./Data/Downloads/TWRP", pTWRPFileName));
                    }
                    else
                    {
                        statusProgress.IsIndeterminate = true;
                        bool lMd5Result = await Task.Run(() => checkTWRPMd5());
                        if (lMd5Result == true)
                        {
                            var matchResult = await this.ShowMessageAsync("MD5 Match", string.Format("The MD5s have returned as a match. Are you ready to flash {0}?", pTWRPFileName), MessageDialogStyle.AffirmativeAndNegative, md5matchSettings);
                            if (matchResult == MessageDialogResult.Affirmative)
                            {
                                IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                                if (state == IDDeviceState.DEVICE || state == IDDeviceState.RECOVERY)
                                {
                                    var controllerRebootingRecoveryFlash = await this.ShowProgressAsync("Waiting for device...", "");
                                    controllerRebootingRecoveryFlash.SetIndeterminate();
                                    cAppend("Waiting for device...");
                                    await Task.Run(() => ADB.WaitForDevice());
                                    cAppend("Rebooting to the bootloader.");
                                    await Task.Run(() => ADB.Instance().Reboot(IDBoot.BOOTLOADER));
                                    await controllerRebootingRecoveryFlash.CloseAsync();
                                    flashTWRP();
                                }
                                else if (state == IDDeviceState.FASTBOOT)
                                {
                                    flashTWRP();
                                }
                                else
                                {
                                    cAppend("Your device is in the wrong state. Please put your device in the bootloader.\n");
                                }
                            }
                            else
                            {
                                cAppend("No devices were detected...\n");
                            }
                        }
                        else
                        {
                            var result = await this.ShowMessageAsync("MD5 Check Failed", "The MD5s are not the same! This means the file is corrupt and probably wasn't downloaded properly. Would you like to re-download (recommended)?",
                                MessageDialogStyle.AffirmativeAndNegative, md5mismatchSettings);
                            if (result == MessageDialogResult.Negative)
                            {
                                File.Delete(Path.Combine("./Data/Downloads/TWRP", pTWRPFileName));
                                btnFlashTwrp_Click(new object(), new RoutedEventArgs());
                            }
                            else
                            {
                                IDDeviceState state = General.CheckDeviceState(ADB.Instance().DeviceID);
                                if (state == IDDeviceState.DEVICE || state == IDDeviceState.RECOVERY)
                                {
                                    var controllerRebootingRecoveryFlash = await this.ShowProgressAsync("Waiting for device...", "");
                                    controllerRebootingRecoveryFlash.SetIndeterminate();
                                    cAppend("Waiting for device...");
                                    await Task.Run(() => ADB.WaitForDevice());
                                    cAppend("Rebooting to the bootloader.");
                                    await Task.Run(() => ADB.Instance().Reboot(IDBoot.BOOTLOADER));
                                    await controllerRebootingRecoveryFlash.CloseAsync();
                                    flashTWRP();
                                }
                                else if (state == IDDeviceState.FASTBOOT)
                                {
                                    flashTWRP();
                                }
                                else
                                {
                                    cAppend("Your device is in the wrong state. Please put your device in the bootloader.\n");
                                }
                            }
                        }
                        statusProgress.IsIndeterminate = false;
                    }
                }
            }
        }

        private async void statusProgress_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };            

            if (_twrpClient != null)
            {
                if (_twrpClient.IsBusy == true)
                {
                    var result = await this.ShowMessageAsync("Cancel Pending Download?", "Are you sure you want to cancel the current download?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        _twrpClient.CancelAsync();
                        _twrpClient.Dispose();
                    }
                }
            }

            if (_suClient != null)
            {
                if (_suClient.IsBusy == true)
                {
                    var result = await this.ShowMessageAsync("Cancel Pending Download?", "Are you sure you want to cancel the current download?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        _suClient.CancelAsync();
                        _suClient.Dispose();
                    }
                }
            }

            if (_driverClient != null)
            {
                if (_driverClient.IsBusy == true)
                {
                    var result = await this.ShowMessageAsync("Cancel Pending Download?", "Are you sure you want to cancel the current download?", MessageDialogStyle.AffirmativeAndNegative, mySettings);
                    if (result == MessageDialogResult.Affirmative)
                    {
                        _driverClient.CancelAsync();
                        _driverClient.Dispose();
                    }
                }
            }
        }

        private async void pushSU_Click(object sender, RoutedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            var noImageSettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "OK",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            if (supersuBuildList.SelectedIndex == -1)
            {
                cAppend("No SuperSU selected...");
                await this.ShowMessageAsync("No SuperSU version selected", "Please select a version of SuperSU and try again.", MessageDialogStyle.Affirmative, noImageSettings);
                supersuBuildList.IsDropDownOpen = true;
            }
            if (supersuBuildList.SelectedIndex == 0)
            {
                // Create OpenFileDialog 
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                // Set filter for file extension and default file extension and title
                dlg.Title = "Open file for SuperSU";
                dlg.DefaultExt = ".zip";
                dlg.Filter = "SuperSU ZIP (*.zip)|*.zip";

                // Display OpenFileDialog by calling ShowDialog method 
                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    // Open document 
                    string filename = dlg.FileName;
                    var controllerPushSU = await this.ShowProgressAsync(string.Format("Pushing {0}", Path.GetFileName(filename)), "");
                    controllerPushSU.SetIndeterminate();
                    statusProgress.IsIndeterminate = true;
                    await Task.Run(() => ADB.Instance().PushPullUTF8.Push(filename, "/sdcard/"));
                    await controllerPushSU.CloseAsync();
                    statusProgress.IsIndeterminate = false;
                }
            }
            else if (supersuBuildList.SelectedIndex != -1)
            {
                IniFileName iniSu = new IniFileName("./Data/.cached/SuBuildList.ini");
                string[] sectionValues = iniSu.GetEntryNames("Nexus 6P (Huawei Nexus 6P)");
                string entryValue = iniSu.GetEntryValue("Nexus 6P (Huawei Nexus 6P)", supersuBuildList.SelectedValue.ToString()).ToString();

                suListStrLineElements = entryValue.Split(';').ToList();

                //Set required variables from ini
                suVersion = suListStrLineElements[0];
                suType = suListStrLineElements[1];

                switch (justPushSU.IsChecked)
                {
                    case true:
                        {
                            suManInstall = true;
                        }
                        break;
                    case false:
                        {
                            suManInstall = false;
                        }
                        break;
                }

                pSuFileName = string.Format("{1}-SuperSU-v{0}.zip", suVersion, suType);

                if (_suClient != null && _suClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }
                else if (_twrpClient != null && _twrpClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }                
                else if (_driverClient != null && _driverClient.IsBusy == true)
                {
                    await this.ShowMessageAsync("Download in progress...",
                        "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                        MessageDialogStyle.Affirmative, mySettings);
                }
                else
                {
                    //Start checking for existing tgz
                    if (!File.Exists(Path.Combine("./Data/Downloads/SU", pSuFileName)))
                    {
                        //Start downloading selected tgz
                        cAppend(string.Format("Downloading {0}. Please wait... You can continue to use the program while I download it in the background.", pSuFileName));
                        cAppend("You can cancel the download by double clicking the progress bar at any time!\n");
                        //Declare new webclient
                        _suClient = new WebClient();
                        //Subscribe to download and completed event handlers
                        _suClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                        _suClient.DownloadFileCompleted += new AsyncCompletedEventHandler(suClient_DownloadFileCompleted);
                        // Starts the download
                        _suClient.DownloadFileAsync(new Uri(string.Format("https://s.basketbuild.com/dl/devs?dl=squabbi/superSU/{0}", pSuFileName)), Path.Combine("./Data/Downloads/SU", pSuFileName));
                    }
                    else
                    {
                        flashSuperSU();
                    }
                }
            }
        }

        private async void adbVersion_Click(object sender, RoutedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            await this.ShowMessageAsync("ADB Version", ADB.Version, MessageDialogStyle.Affirmative, mySettings);
        }

        private async void adbSideload_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension and title
            dlg.Title = "Open file for Sideloading";
            dlg.DefaultExt = ".zip";
            dlg.Filter = "ZIP Archives (*.zip)|*.zip";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                //MessageBox.Show(filename);
                var controllerSideload = await this.ShowProgressAsync("Sideloading", filename);
                controllerSideload.SetIndeterminate();

                await Task.Run(() => ADB.Instance().Sideload(filename));

                await controllerSideload.CloseAsync();
            }
        }

        private async void adbRebootReboot_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => ADB.Instance().Reboot(IDBoot.REBOOT));
        }

        private async void adbRebootBootloader_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => ADB.Instance().Reboot(IDBoot.BOOTLOADER));
        }

        private async void adbRebootRecovery_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => ADB.Instance().Reboot(IDBoot.RECOVERY));
        }

        private async void adbAPK_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension and title
            dlg.Multiselect = true;
            dlg.Title = "Open file for Installing APK";
            dlg.DefaultExt = ".apk";
            dlg.Filter = "APK Package (*.apk)|*.apk";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                statusProgress.IsIndeterminate = true;
                // Open document 
                foreach (string apk in dlg.FileNames)
                {
                    cAppend(string.Format("Installing {0}...", Path.GetFileName(apk)));
                    bool apkInstalled = await Task.Run(() => ADB.Instance().Install(apk));
                    if (apkInstalled == true)
                    {
                        cAppend(string.Format("Sucessfully installed {0}.", Path.GetFileName(apk)));
                    }
                    else if (apkInstalled == false)
                    {
                        cAppend(string.Format("Failed to install {0}.", Path.GetFileName(apk)));
                    }
                    else
                    {
                        cAppend("No return for installing APK...\n");
                    }
                }
                cAppend("Finished installing APK(s).\n");
                statusProgress.IsIndeterminate = false;
            }
        }

        private async void fbtRebootReboot_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Fastboot.Instance().Reboot(IDBoot.REBOOT));
        }

        private async void fbtRebootBootloader_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => Fastboot.Instance().Reboot(IDBoot.BOOTLOADER));
        }

        private void linkDeviceManager_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("devmgmt.msc");
        }

        private void linkExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void saveLog_Click(object sender, RoutedEventArgs e)
        {
            // Text from the rich textbox rtfMain
            string str = console.Document.ToString();
            // Create OpenFileDialog 
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            // Create a new SaveFileDialog object
            try
            {
                // Available file extensions
                dlg.Filter = "All Files (*.*)|*.*";
                // SaveFileDialog title
                dlg.Title = "Save Console Output";
                // Show SaveFileDialog
                if (dlg.ShowDialog() == true)
                {
                    TextRange range;
                    FileStream fStream;
                    range = new TextRange(console.Document.ContentStart, console.Document.ContentEnd);
                    fStream = new FileStream(dlg.FileName, FileMode.Create);
                    range.Save(fStream, DataFormats.Text);
                    fStream.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "An error has occured while attempting to save the output...");
            }
        }

        private void xdaThread_Click(object sender, RoutedEventArgs e)
        {
            cAppend("Attempted opening XDA thread");
            Process.Start("http://forum.xda-developers.com/showthread.php?t=2694925");

        }

        private void openDataFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(appPath + "/Data/");
        }

        private async void installDrivers_Click(object sender, RoutedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Yes",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            //Download driver
            if (_suClient != null && _suClient.IsBusy == true)
            {
                await this.ShowMessageAsync("Download in progress...",
                    "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                    MessageDialogStyle.Affirmative, mySettings);
            }
            else if (_twrpClient != null && _twrpClient.IsBusy == true)
            {
                await this.ShowMessageAsync("Download in progress...",
                    "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                    MessageDialogStyle.Affirmative, mySettings);
            }
            else if (_driverClient != null && _driverClient.IsBusy == true)
            {
                await this.ShowMessageAsync("Download in progress...",
                    "A download is already in progress! I cannot start another one, please wait for the exisiting download to finish or cancel it by double-clicking the progress bar.",
                    MessageDialogStyle.Affirmative, mySettings);
            }
            else
            {
                //Start checking for existing driver
                if (!File.Exists("./Data/Downloads/HTC Driver.msi"))
                {
                    //Start downloading selected driver
                    cAppend("Downloading HTC Driver.msi. Please wait... You can continue to use the program while I download it in the background.");
                    cAppend("You can cancel the download by double clicking the progress bar at any time!\n");
                    //Declare new webclient
                    _driverClient = new WebClient();
                    //Subscribe to download and completed event handlers
                    _driverClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                    _driverClient.DownloadFileCompleted += new AsyncCompletedEventHandler(driverClient_DownloadFileCompleted);
                    // Starts the download
                    _driverClient.DownloadFileAsync(new Uri(string.Format("https://s.basketbuild.com/dl/devs?dl=squabbi/HTCDriver.msi")), "./Data/Downloads/HTC Driver.msi");
                }
                else
                {
                    try
                    {
                        Process.Start(appPath + "/Data/Downloads/HTC Driver.msi");
                    }
                    catch (Exception ex)
                    {
                        await this.ShowMessageAsync("Unable to launch driver installer", "Please try again. If this persists, please make sure you/the toolkit has sufficent permissions.");
                    }
                }
            }
        }

        private async void driverClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            //Check if it was cancled
            if (e.Cancelled)
            {
                //Clean up
                _driverClient.Dispose();
                //Set progress to 0
                statusProgress.Value = 0;
                retryLvl = 0;
                cAppend("Download cancelled!");
                cAppend("Cleaning up...\n");
                //Delete attemped file download
                if (File.Exists("./Data/Downloads/HTC Driver.msi"))
                {
                    cAppend("Deleting HTC Driver.msi");
                    cAppend("Ready for next download...\n");
                    File.Delete("./Data/Downloads/HTC Driver.msi");
                }
            }
            //In case of error
            if (e.Error != null)
            {
                // We have an error! Retry a few times, then abort.
                retryLvl++;
                if (retryLvl < 3)
                {
                    cAppend(string.Format("Failed.. {0} retry...", retryLvl.ToString()));
                    cAppend(string.Format("Error: {0}\n", e.Error.Message));
                    installDrivers_Click(new object(), new RoutedEventArgs());
                }
                else
                {
                    retryLvl = 0;
                    cAppend(string.Format("Failed after 3 retries... Error: {0}", e.Error.Message));
                }
            }
            //No error
            if (!e.Cancelled)
            {
                //Clean Up
                _driverClient.Dispose();
                //Progress 0
                statusProgress.Value = 0;
                retryLvl = 0;
                //Start installing
                try
                {
                    Process.Start(appPath + "/Data/Downloads/HTC Driver.msi");
                }
                catch (Exception ex)
                {
                    var dictionary = new ResourceDictionary();
                    dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

                    var mySettings = new MetroDialogSettings
                    {
                        AffirmativeButtonText = "OK",
                        NegativeButtonText = "Try Again",
                        SuppressDefaultResources = true,
                        CustomResourceDictionary = dictionary
                    };

                    await this.ShowMessageAsync("Could not execute HTC Driver.msi",
                    "There could be a read error (permissions), if the error persists, try running it manually.\n\n" + ex,
                    MessageDialogStyle.Affirmative, mySettings);                   
                }
            }
        }

        private void openTwrpFaq_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://forum.xda-developers.com/one-m9/orig-development/recovery-twrp-touch-recovery-t3066720/post59745198#post59745198");
        }

        private async void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/MaterialDesignThemes.MahApps;component/Themes/MaterialDesignTheme.MahApps.Dialogs.xaml");

            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "OK",
                NegativeButtonText = "No",
                SuppressDefaultResources = true,
                CustomResourceDictionary = dictionary
            };

            await this.ShowMessageAsync("About", "- HTC One M8 Toolkit\n\n" +
                "I made this as a little handy tool for your lovely, shiny M8. This offers " +
                "a little less than the previous version but will be made to work in the future!\n" +
                "This is only possible with XDA Seinor Memeber @k1ll3r8e with his AndroidCtrl library\n" +
                "framework. Special thanks to him.\n\n" +
                "Thanks to http://icons8.com/ for the icons in the application.\n\n" +
                "The rest was me and of course the community for their feedback and inspriation.\n\n" +
                "Thank you for using my toolkit! :)\n" +
                "~Squabbi @ XDA", MessageDialogStyle.Affirmative, mySettings);
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            //Stop Connection Monitor before close
            //Be sure to dispose of ADB and Fastboot for the Application to terminate properly
            ADB.ConnectionMonitor.Stop();
            ADB.ConnectionMonitor.Callback -= ConnectionMonitorCallback;
            ADB.Stop();
            Fastboot.Dispose();
            ADB.Dispose();
        }

        private async void adbRebootDownload_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => ADB.Instance().Reboot(IDBoot.DOWNLOAD));
        }
    }
}
