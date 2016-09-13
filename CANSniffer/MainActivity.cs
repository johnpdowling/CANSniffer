using Android.App;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.InputMethodServices;
using Android.Views;
using Android.Views.InputMethods;
using Android.Views.Animations;
using System;

namespace CANSniffer
{
	[Activity(Label = "CANSniffer", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		DateTime sniffStart;

		BluetoothSerialPort port;
		CANInterpreter interpreter = new CANInterpreter();

		bool sniffing = false;

		List<string> messages = new List<string>();
		List<string> messageData = new List<string>();
		string seperator = "::";

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);

			ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
			myview.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1, messages);

			interpreter.CANMessageReceived += CANMessageReceived;
			interpreter.CANMaskSettingReceived += CANMaskSettingReceived;
			interpreter.CANFilterSettingReceived += CANFilterSettingReceived;

			KeyboardView keyboardView = (KeyboardView)FindViewById<KeyboardView>(Resource.Id.keyboardview);
			keyboardView.Keyboard = new Keyboard(this, Resource.Xml.hexkeyboard);
			keyboardView.PreviewEnabled = false;
			keyboardView.OnKeyboardActionListener = new HexKeyboardListener(this);



			Button button = FindViewById<Button>(Resource.Id.prefsButton);

			button.Click += delegate 
			{
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				List<string> descriptions = new List<string>();
				foreach (BluetoothDevice device in BluetoothSerialPort.BondedSerialDevices)
				{
					descriptions.Add(device.Name + " - " + device.Address);
				}

				alert.SetTitle("Choose SPP device:");
				alert.SetItems(descriptions.ToArray(),
				async (sender, e) =>
				{
					string deviceDescription = descriptions[e.Which];
					string[] descripSplit = deviceDescription.Split(" - ".ToCharArray());
					string mac = descripSplit[descripSplit.Length - 1];
					if (mac != null)
					{
						Task serialTask = StartSerial(mac);
						SavePreferences();
						await serialTask;
					}
				});

				alert.SetNegativeButton("Cancel", (senderAlert, args) =>
				{
					Toast.MakeText(this, "Cancelled!", ToastLength.Short).Show();
				});

				Dialog dialog = alert.Create();
				dialog.Show();
			};

			button = FindViewById<Button>(Resource.Id.startstopButton);
			button.Click += StartStopButton_Click;

			setEditText(Resource.Id.mask1EditText);
			setEditText(Resource.Id.mask2EditText);
			setEditText(Resource.Id.filter1EditText);
			setEditText(Resource.Id.filter2EditText);
			setEditText(Resource.Id.filter3EditText);
			setEditText(Resource.Id.filter4EditText);
			setEditText(Resource.Id.filter5EditText);
			setEditText(Resource.Id.filter6EditText);

			this.Window.SetSoftInputMode(SoftInput.StateAlwaysHidden);

			LoadPreferences();
		}

		private void setEditText(int resourceID)
		{
			EditText toSet = FindViewById<EditText>(resourceID);
			toSet.OnFocusChangeListener = new HexKeyboardOnFocusChangeListener(this);
			toSet.SetOnClickListener(new HexKeyboardOnClickListener(this));
			toSet.InputType = Android.Text.InputTypes.Null;

			toSet.KeyPress += async (sender, e) =>
			{
				switch ((Android.Views.Keycode)e.KeyCode)
				{
					case Android.Views.Keycode.Clear:
						(sender as EditText).Text = "";
						break;
					case Android.Views.Keycode.Del:
						(sender as EditText).Text =
							(sender as EditText).Text != "" ?
							(sender as EditText).Text.Substring(0, (sender as EditText).Text.Length - 1) :
							"";
						break;
					case Android.Views.Keycode.Enter:
						ushort setting = ((sender as EditText).Text != "" ?
										  System.Convert.ToUInt16((sender as EditText).Text, 16) :
										  (ushort)0x1000);
						Task setTask = null;
						switch ((sender as EditText).Id)
						{
							case Resource.Id.mask1EditText:
								setTask = interpreter.SendNewMask(0, setting);
								break;
							case Resource.Id.mask2EditText:
								setTask = interpreter.SendNewMask(1, setting);
								break;
							case Resource.Id.filter1EditText:
								setTask = interpreter.SendNewFilter(0, setting);
								break;
							case Resource.Id.filter2EditText:
								setTask = interpreter.SendNewFilter(1, setting);
								break;
							case Resource.Id.filter3EditText:
								setTask = interpreter.SendNewFilter(2, setting);
								break;
							case Resource.Id.filter4EditText:
								setTask = interpreter.SendNewFilter(3, setting);
								break;
							case Resource.Id.filter5EditText:
								setTask = interpreter.SendNewFilter(4, setting);
								break;
							case Resource.Id.filter6EditText:
								setTask = interpreter.SendNewFilter(5, setting);
								break;
						}
						((sender as EditText).OnFocusChangeListener as HexKeyboardOnFocusChangeListener).Set();
						(sender as EditText).ClearFocus();
						await setTask;
						break;
					case Android.Views.Keycode.Num0:
					case Android.Views.Keycode.Num1:
					case Android.Views.Keycode.Num2:
					case Android.Views.Keycode.Num3:
					case Android.Views.Keycode.Num4:
					case Android.Views.Keycode.Num5:
					case Android.Views.Keycode.Num6:
					case Android.Views.Keycode.Num7:
					case Android.Views.Keycode.Num8:
					case Android.Views.Keycode.Num9:
						(sender as EditText).Text += e.KeyCode.ToString().Substring(e.KeyCode.ToString().Length - 1);
						break;
					default:
						(sender as EditText).Text += e.KeyCode.ToString().ToUpper();
						break;
				}
			};
		}

		public void ShowHexKeyboard(View v)
		{
			var bottomUp = AnimationUtils.LoadAnimation(this, Resource.Xml.slideup);
			KeyboardView keyboardView = (KeyboardView)FindViewById<KeyboardView>(Resource.Id.keyboardview);
			keyboardView.StartAnimation(bottomUp); 
			keyboardView.Visibility = ViewStates.Visible;
			keyboardView.Enabled = true;
			if (v != null)
			{
				((InputMethodManager)GetSystemService(InputMethodService)).HideSoftInputFromWindow(v.WindowToken, 0);
			}
		}

		public void HideHexKeyboard()
		{
			KeyboardView keyboardView = (KeyboardView)FindViewById<KeyboardView>(Resource.Id.keyboardview);
			keyboardView.Visibility = ViewStates.Gone;
			keyboardView.Enabled = false;
		}

		private void BluetoothConnectionReceived(object sender, BluetoothConnectionReceivedEventArgs args)
		{
			if (args.Connected)
			{
				RunOnUiThread(() =>
				{
					TextView deviceview = FindViewById<TextView>(Resource.Id.deviceNameTextView);
					deviceview.Text = args.Name;
					TextView connectview = FindViewById<TextView>(Resource.Id.connectionTextView);
					connectview.Text = (args.Connected ? "Connected" : "Not Connected");

				});
				interpreter.Port = port;
				Task.Run(() => interpreter.SendHeartbeat());
				Task.Run(() => interpreter.InterpretStream());
			}
		}

		private void CANMessageReceived(object sender, CANMessageReceivedEventArgs args)
		{
			if (sniffing)
			{
				string message = "ID: " + args.CANID.ToString("X3");
				message += " Message:";
				string dataMessage = (DateTime.Now - sniffStart).TotalMilliseconds.ToString();
				dataMessage += seperator + "\"" + args.CANID.ToString("X3") + "\"";
				for (byte i = 0; i < args.Message.Length; i++)
				{
					message += " " + args.Message[i].ToString("X2");
					dataMessage += seperator + "\"" + args.Message[i].ToString("X2") + "\"";
				}
				for (byte i = (byte)args.Message.Length; i < 8; i++)
				{
					dataMessage += seperator +"\"\"";
				}
				message += " ";
				for (byte i = 0; i < args.Message.Length; i++)
				{
					message += (IsDisplayableCharacter((char)args.Message[i]) ? ((char)args.Message[i]).ToString() : ".");
					dataMessage += seperator + "\"" + (IsDisplayableCharacter((char)args.Message[i]) ? ((char)args.Message[i]).ToString() : ".") + "\"";
				}
				messageData.Add(dataMessage);
				RunOnUiThread(() =>
				{
					ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
					(myview.Adapter as ArrayAdapter).Add(message);
				});
			}
		}

		void CANFilterSettingReceived(object sender, CANSniffer.CANSettingReceivedEventArgs e)
		{
			EditText text = null;
			switch (e.Index)
			{
				case 0:
					text = FindViewById<EditText>(Resource.Id.filter1EditText);
					break;
				case 1:
					text = FindViewById<EditText>(Resource.Id.filter2EditText);
					break;
				case 2:
					text = FindViewById<EditText>(Resource.Id.filter3EditText);
					break;
				case 3:
					text = FindViewById<EditText>(Resource.Id.filter4EditText);
					break;
				case 4:
					text = FindViewById<EditText>(Resource.Id.filter5EditText);
					break;
				case 5:
					text = FindViewById<EditText>(Resource.Id.filter6EditText);
					break;
			}
			if (text != null)
			{
				if (e.Setting < 0x1000)
				{
					if (text.Text != e.Setting.ToString("X3"))
					{
						RunOnUiThread(() =>
						{
							text.Text = e.Setting.ToString("X3");
						});
					}
				}
				else
				{
					RunOnUiThread(() =>
					{
						text.Text = "";
					});
				}
			}
		}

		void CANMaskSettingReceived(object sender, CANSniffer.CANSettingReceivedEventArgs e)
		{
			EditText text = null;
			switch (e.Index)
			{
				case 0:
					text = FindViewById<EditText>(Resource.Id.mask1EditText);
					break;
				case 1:
					text = FindViewById<EditText>(Resource.Id.mask2EditText);
					break;
			}
			if (text != null)
			{
				if (e.Setting < 0x1000)
				{
					if (text.Text != e.Setting.ToString("X3"))
					{
						RunOnUiThread(() =>
						{
							text.Text = e.Setting.ToString("X3");
						});
					}
				}
				else
				{
					RunOnUiThread(() =>
					{
						text.Text = "";
					});
				}
			}
		}

		private bool IsDisplayableCharacter(char c)
		{
			//keyboard characters + space
			return char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c) || c == ' ';
		}

		private async Task StartSerial(string mac)
		{
			if (port != null)
			{
				port.BluetoothConnectionReceived -= BluetoothConnectionReceived;
				port = null;
			}
			port = new BluetoothSerialPort(mac);
			port.BluetoothConnectionReceived += BluetoothConnectionReceived;
			Task connectTask = port.ConnectAsync();
			RunOnUiThread(() =>
			{
				TextView deviceview = FindViewById<TextView>(Resource.Id.deviceNameTextView);
				deviceview.Text = port.Name;
				TextView connectview = FindViewById<TextView>(Resource.Id.connectionTextView);
				connectview.Text = "Connecting...";

			});
			await connectTask;
		}

		private void SavePreferences()
		{
			var prefs = Application.Context.GetSharedPreferences("CANSniffer", FileCreationMode.Private);
			var prefEditor = prefs.Edit();
			prefEditor.PutString("DeviceMAC", port.Address);
			prefEditor.Commit();
		}

		private void LoadPreferences()
		{
			//retreive 
			var prefs = Application.Context.GetSharedPreferences("CANSniffer", FileCreationMode.Private);
			var macPref = prefs.GetString("DeviceMAC", null);

			if (macPref != null)
			{
				Task.Run(() => StartSerial(macPref));
			}
		}

		void StartStopButton_Click(object sender, System.EventArgs e)
		{
			sniffing = !sniffing;
			if (!sniffing)
			{
				//finished sniff. save?
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				alert.SetTitle("Save Session?");
				alert.SetPositiveButton("Save", (senderAlert, args) =>
				{
					string sdFolderPath = System.IO.Path.Combine("/storage/sdcard1", "CANSnifferLogs");
					string filename = "Log" + sniffStart.Month.ToString("D2") + "-" + sniffStart.Day.ToString("D2") + "-" + sniffStart.Year.ToString() + "_" +
											  sniffStart.Hour.ToString("D2") + "," + sniffStart.Minute.ToString("D2") + "," + sniffStart.Second.ToString("D2") + ".csv";
					string filePath = System.IO.Path.Combine(sdFolderPath, filename);
					if (!System.IO.File.Exists(filePath))
					{
						try
						{
							using (System.IO.StreamWriter write = new System.IO.StreamWriter(filePath, true))
							{
								write.WriteLine("Mask1" + seperator + "Mask2");
								write.WriteLine(FindViewById<EditText>(Resource.Id.mask1EditText).Text + seperator +
												 FindViewById<EditText>(Resource.Id.mask2EditText).Text);
								write.WriteLine("Filter1" + seperator + "Filter2" + seperator + "Filter3" + seperator + "Filter4" + seperator + "Filter5" + seperator + "Filter6");
								write.WriteLine(FindViewById<EditText>(Resource.Id.filter1EditText).Text + seperator +
												FindViewById<EditText>(Resource.Id.filter2EditText).Text + seperator +
										    	FindViewById<EditText>(Resource.Id.filter3EditText).Text + seperator +
												FindViewById<EditText>(Resource.Id.filter4EditText).Text + seperator +
										   		FindViewById<EditText>(Resource.Id.filter5EditText).Text + seperator +
												FindViewById<EditText>(Resource.Id.filter6EditText).Text);
								write.WriteLine("Time" + seperator + "CANID" + seperator + "BYTE0" + seperator + "BYTE1" + seperator + "BYTE2" + seperator + "BYTE3" + seperator + 
								                										   "BYTE4" + seperator + "BYTE5" + seperator + "BYTE6" + seperator + "BYTE7" + seperator +
								               											   "TEXT0" + seperator + "TEXT1" + seperator + "TEXT2" + seperator + "TEXT3" + seperator +
																						   "TEXT4" + seperator + "TEXT5" + seperator + "TEXT6" + seperator + "TEXT7");
								foreach (string data in messageData)
								{
									write.WriteLine(data);
								}
							}
						}
						catch
						{
							Toast.MakeText(this, "Error Saving Data", ToastLength.Short).Show();
						}
					}
					Toast.MakeText(this, "Session Data Saved", ToastLength.Short).Show();
				});
				alert.SetNegativeButton("Discard", (senderAlert, args) =>
				{
					Toast.MakeText(this, "Session Data Discarded", ToastLength.Short).Show();
				});

				Dialog dialog = alert.Create();
				dialog.Show();
			}
			else
			{
				messageData.Clear();
				RunOnUiThread(() =>
				{
					ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
					(myview.Adapter as ArrayAdapter).Clear();
				});
				sniffStart = DateTime.Now;
			}
			Button button = FindViewById<Button>(Resource.Id.startstopButton);
			button.Text = GetString(sniffing ? Resource.String.stop : Resource.String.start);
		}

		public override void OnBackPressed()
		{
			KeyboardView keyboardView = (KeyboardView)FindViewById<KeyboardView>(Resource.Id.keyboardview);
			if (keyboardView.Visibility == ViewStates.Visible)
			{
				HideHexKeyboard();
			}
			else
			{
				base.OnBackPressed();
			}
		}
	}
}


