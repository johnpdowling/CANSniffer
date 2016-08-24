using Android.App;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;

namespace CANSniffer
{
	[Activity(Label = "CANSniffer", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		BluetoothSerialPort port;
		CANInterpreter interpreter = new CANInterpreter();

		bool sniffing = false;

		List<string> messages = new List<string>();

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView(Resource.Layout.Main);

			ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
			myview.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1, messages);

			interpreter.CANMessageReceived += CANMessageReceived;

			LoadPreferences();

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
				Task.Run(() => interpreter.InterpretStream());
			}
		}

		private void CANMessageReceived(object sender, CANMessageReceivedEventArgs args)
		{
			if (sniffing)
			{
				string message = "ID: " + args.CANID.ToString("X3");
				message += " Message:";
				for (byte i = 0; i < args.Message.Length; i++)
				{
					message += " 0x" + args.Message[i].ToString("X2");
				}
				RunOnUiThread(() =>
				{
					ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
					(myview.Adapter as ArrayAdapter).Add(message);
				});
			}
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
			Button button = FindViewById<Button>(Resource.Id.startstopButton);
			button.Text = GetString(sniffing ? Resource.String.stop : Resource.String.start);
		}
	}
}


