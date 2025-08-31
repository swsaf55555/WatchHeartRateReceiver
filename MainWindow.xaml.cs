using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace WatchHeartRateReceiver
{
    public partial class MainWindow : Window
    {
        private BluetoothLEAdvertisementWatcher watcher;
        private static ulong latestHeartRate = 0;
        private static string latestDevice = "";
        private static string latestDeviceMac = "";
        private static ulong latestDeviceMacLong= 0;
        private static short latestRssi = 0;
        private static short lastValidRssi = 0;
        private static string latestDeviceLongName = "";
        private static readonly string scan_old_device = "scan_old_device";
        private static readonly string scan_new_device = "scan_new_device";
        private static readonly string scan_stop = "stop";
        private static readonly string mode_select = "select";
        private static readonly string mode_stop = "delete";
        //private static string mode_show = "show";
        private static readonly string mode_scan = "scan";
        private string mode = mode_stop;
        private string scan_type=scan_stop;
        private static readonly Dictionary<ulong, BluetoothLEDevice> devices = [];
        private static string statu_text = "";
        private static string device_selected = "";

        public MainWindow()
        {
            InitializeComponent();
            var factory = new FrameworkElementFactory(typeof(ContentPresenter));
            factory.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");

            var template = new ControlTemplate(typeof(TabControl));
            template.VisualTree = factory;

            tabControl.Template = template;
            tabControl.SelectedIndex = 0;
            StartScanner();
            StartUiUpdater();
            _ = RestartWatcherPeriodically();
        }



        private void StartScanner()
        {
            watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            // 调整信号过滤器
            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -100;
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -110; 
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromSeconds(5); // 丢 5 秒才认为掉线

            watcher.Received += (sender, args) =>
            {
                if (scan_type != scan_stop)
                {
                    if (mode == mode_select)
                    {
                        try
                        {
                            latestDevice = args.Advertisement.LocalName;
                            if (string.IsNullOrEmpty(latestDevice))
                            {
                                latestDevice = "未知设备名";
                            }
                            latestDeviceMacLong = args.BluetoothAddress;
                            latestDeviceMac = string.Join(":",
                                BitConverter.GetBytes(args.BluetoothAddress)
                                            .Reverse()
                                            .Take(6)
                                            .Select(b => b.ToString("X2"))
                            );
                            latestDeviceLongName = latestDevice + " - " + latestDeviceMac;
                        }
                        catch (Exception e)
                        {
                            statu_text = "扫描出现错误:" + e;

                        }
                    }
                    else if (mode==mode_scan&&scan_type == scan_old_device)
                    {
                        try
                        {
                            short rssi = args.RawSignalStrengthInDBm;

                            // 过滤 -127 (信号无效)
                            if (rssi != -127)
                            {
                                latestRssi = rssi;
                                lastValidRssi = rssi; // 保存最近一次有效值
                            }
                            else
                            {
                                latestRssi = lastValidRssi; // 用旧值代替
                            }

                            // 获取设备名和地址
                            latestDevice = args.Advertisement.LocalName;
                            if (string.IsNullOrEmpty(latestDevice))
                            {
                                latestDevice = "未知设备名";
                            }
                            latestDeviceMacLong = args.BluetoothAddress;
                            latestDeviceMac = string.Join(":",
                                BitConverter.GetBytes(args.BluetoothAddress)
                                            .Reverse()
                                            .Take(6)
                                            .Select(b => b.ToString("X2"))
                            );
                            latestDeviceLongName = latestDevice + " - " + latestDeviceMac;

                            // 解析心率数据
                            foreach (var md in args.Advertisement.ManufacturerData)
                            {
                                if (md.CompanyId != 0x0157) continue;

                                var reader = DataReader.FromBuffer(md.Data);
                                byte[] buffer = new byte[reader.UnconsumedBufferLength];
                                reader.ReadBytes(buffer);

                                if (buffer.Length > 3)
                                {
                                    if (buffer[3] == 0xff)
                                    {
                                        latestHeartRate = 0;
                                    }
                                    else
                                    {
                                        latestHeartRate = buffer[3];
                                    }
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            statu_text = "扫描出现错误:" + e;

                        }
                    }
                    else if (mode == mode_scan && scan_type == scan_new_device)
                    {
                        try
                        {
                            short rssi = args.RawSignalStrengthInDBm;

                            // 过滤 -127 (信号无效)
                            if (rssi != -127)
                            {
                                latestRssi = rssi;
                                lastValidRssi = rssi; // 保存最近一次有效值
                            }
                            else
                            {
                                latestRssi = lastValidRssi; // 用旧值代替
                            }

                            // 获取设备名和地址
                            latestDevice = args.Advertisement.LocalName;
                            if (string.IsNullOrEmpty(latestDevice))
                            {
                                latestDevice = "未知设备名";
                            }
                            latestDeviceMacLong = args.BluetoothAddress;
                            latestDeviceMac = string.Join(":",
                                BitConverter.GetBytes(args.BluetoothAddress)
                                            .Reverse()
                                            .Take(6)
                                            .Select(b => b.ToString("X2"))
                            );
                            latestDeviceLongName = latestDevice + " - " + latestDeviceMac;
                            Task.Run(async () => await ParseNewDeviceAsync(args));
                        }
                        catch (Exception e)
                        {
                            statu_text = "扫描出现错误:" + e;
                        }

                    }
                }
                
            };

            watcher.Start();
            statu_text = "扫描已启动";
        }


        private async Task ParseNewDeviceAsync(BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                string add= latestDeviceMac = string.Join(":",
                                BitConverter.GetBytes(args.BluetoothAddress)
                                            .Reverse()
                                            .Take(6)
                                            .Select(b => b.ToString("X2"))
                            );
                if (args.Advertisement.LocalName + " - " + add != device_selected) return;
                if (devices.ContainsKey(args.BluetoothAddress)) return;
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
                if (device == null) return;
                devices.Add(device.BluetoothAddress, device);
                var hrServiceResult = await device.GetGattServicesForUuidAsync(BluetoothUuidHelper.FromShortId(0x180D));
                var hrService = hrServiceResult.Services.FirstOrDefault();
                if (hrService == null) return;
                var hrCharResult = await hrService.GetCharacteristicsForUuidAsync(BluetoothUuidHelper.FromShortId(0x2A37));
                var hrCharacteristic = hrCharResult.Characteristics.FirstOrDefault();
                if (hrCharacteristic == null) return;

                hrCharacteristic.ValueChanged += HeartRateValueChanged;
                await hrCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
            }
            catch (Exception e)
            {
                statu_text = "扫描出现错误:" + e;
            }
        }
        private static void HeartRateValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                using var reader = DataReader.FromBuffer(args.CharacteristicValue);
                reader.ByteOrder = ByteOrder.LittleEndian;

                // 读取标志位
                byte flags = reader.ReadByte();
                bool is16bit = (flags & 0x01) != 0;
                var heartRate = is16bit ? reader.ReadUInt16() : reader.ReadByte();
                if (heartRate == 0xff)
                {
                    latestHeartRate = 0;
                }
                else
                {
                    latestHeartRate = Convert.ToByte(heartRate);
                }
            }
            catch(Exception e)
            {
                statu_text = "扫描出现错误:" + e;
            }
        }


        private void StartUiUpdater(){
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100); 
            timer.Tick += (s, e) =>
            {
                StatusText.Dispatcher.Invoke(() =>
                {
                    StatusText.Text = statu_text;
                });
                if (mode == mode_select)
                {
                    if (latestDeviceMacLong != 0)
                    {
                        if (!comboBox_device.Items.Contains(latestDeviceLongName))
                        {
                            comboBox_device.Items.Add(latestDeviceLongName);
                            scan_num.Text = "当前已扫描到" + comboBox_device.Items.Count + "个设备";
                            if (comboBox_device.SelectedIndex == -1)
                            {
                                comboBox_device.SelectedIndex = 0;
                                button_select_2.IsEnabled = true;
                            }
                        }
                    }
                

                }else if(mode == mode_scan)
                {
                    //tabControl.SelectedIndex = 2;
                    if (latestDeviceLongName==device_selected)
                    {
                        DeviceNameText.Text = latestDevice;
                        if(latestHeartRate != 0)
                        {
                            HeartRateText.Text = $"{latestHeartRate} bpm";
                        }
                        else
                        {
                            HeartRateText.Text = "-";
                        }
                        if(latestRssi != 0)
                        {
                            RssiText.Text = $"{latestRssi} dBm";
                        }
                        else{
                            RssiText.Text = "-";
                        }
                                             
                        statu_text = "";
                    }
                    else
                    {
                        statu_text = "";
                    }
                }
           
            };
            timer.Start(); 
        }


        // 定时重启 watcher，防止被挂起
        private async Task RestartWatcherPeriodically()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                watcher.Stop();
                await Task.Delay(500);
                watcher.Start();
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if(radioButton_olddevice.IsChecked == true) {
                scan_type = scan_old_device;
            }else if(radioButton_newdevice.IsChecked == true)
            {
               scan_type=scan_new_device;
            }
            tabControl.SelectedIndex = 1;
            mode = mode_select;
            //StartScanner();
        }

        private void button_select_back_Click(object sender, RoutedEventArgs e)
        {
            tabControl.SelectedIndex = 0;
            scan_type = scan_stop;
            mode= mode_stop;
        }

        private void button_select_2_Click(object sender, RoutedEventArgs e)
        {
            device_selected = comboBox_device.SelectedItem.ToString()??"";
            tabControl.SelectedIndex = 2;
            mode = mode_scan;
            MessageBox.Show("如果心率数据始终不更新，可能有以下原因：\n" +
                "1.心率数据尚未被手表广播，请耐心等待。\n" +
                "2.设备类型选择错误，因此无法读取心率，请退出监控重新选择设备类型。\n" +
                "3.某些设备只有在运动模式下才会广播心率，请开启设备的运动模式。\n" +
                "4.未开启设备的心率广播功能，请手动开启设备心率广播。\n" +
                "5.设备距离过远或已断开连接，请靠近设备或退出监控后重新开启监控。"
                ,"提示");
        }

        private void radioButton_newdevice_Checked(object sender, RoutedEventArgs e)
        {
            button_select.IsEnabled = true;
        }

        private void radioButton_olddevice_Checked(object sender, RoutedEventArgs e)
        {
            button_select.IsEnabled=true;
        }

        private void button_exit_Click(object sender, RoutedEventArgs e)
        {
            mode = mode_select;
            button_select_2.IsEnabled = false;
            device_selected = "";
            comboBox_device.Items.Clear();
            tabControl.SelectedIndex=1;
            latestRssi= 0;
            latestHeartRate= 0;
            latestDeviceMacLong = 0;
            latestDevice = "";
            latestDeviceLongName = "";
            comboBox_device.SelectedIndex = -1;
            scan_num.Text = "当前已扫描到0个设备";
        }
    }
}
