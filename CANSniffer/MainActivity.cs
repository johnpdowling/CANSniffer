using Android.App;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CANSniffer
{
	[Activity(Label = "CANSniffer", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : Activity
	{
		BluetoothSerialPort port;
		CANInterpreter interpreter = new CANInterpreter();

		List<string> messages = new List<string>();

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
			myview.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1, messages);

			interpreter.CANMessageReceived += CANMessageReceived;

			// Get our button from the layout resource,
			// and attach an event to it

			Button button = FindViewById<Button>(Resource.Id.myButton);

			button.Click += delegate 
			{
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				List<string> descriptions = new List<string>();
				foreach (BluetoothDevice device in BluetoothSerialPort.BondedSerialDevices)
				{
					descriptions.Add(device.Name + " - " + device.Address);
				}

				alert.SetTitle("Please choose a SPP device:");
				alert.SetItems(descriptions.ToArray(),
				async (sender, e) =>
				{
					string deviceDescription = descriptions[e.Which];
					string[] descripSplit = deviceDescription.Split(" - ".ToCharArray());
					string mac = descripSplit[descripSplit.Length - 1];
					byte[] macbytes = macStringToBytes(mac);
					if (macbytes != null)
					{
						if (port != null)
						{
							port.BluetoothConnectionReceived -= BluetoothConnectionReceived;
							port = null;
						}
						port = new BluetoothSerialPort(macbytes);
						port.BluetoothConnectionReceived += BluetoothConnectionReceived;
						Task connectTask = port.ConnectAsync();
						button.Text = deviceDescription;
						await connectTask;
					}
				});

				alert.SetNegativeButton("Cancel", (senderAlert, args) =>
				{
					Toast.MakeText(this, "Cancelled!", ToastLength.Short).Show();
				});

				Dialog dialog = alert.Create();
				dialog.Show();
			};
		}

		private void BluetoothConnectionReceived(object sender, BluetoothConnectionReceivedEventArgs args)
		{
			if (args.Connected)
			{
				interpreter.Port = port;
				Task foo = interpreter.InterpretStream();
			}
		}

		private void CANMessageReceived(object sender, CANMessageReceivedEventArgs args)
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

		private byte[] macStringToBytes(string mac)
		{
			if (mac == null) return null;
			string[] macs = mac.Split(":".ToCharArray());
			if (macs.Length != 6)
			{
				return null;
			}
			byte[] bytes = new byte[6];
			for (int i = 0; i < 6; i++)
			{
				try
				{
					bytes[i] = System.Convert.ToByte(macs[i], 16);
				}
				catch
				{
					return null;
				}
			}
			return bytes;
		}
	}
}


