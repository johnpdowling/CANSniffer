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
		BluetoothDevice device;
		BluetoothSocket socket;

		Java.Util.UUID serialPortUUID = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");

		List<string> messages = new List<string>();

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			ListView myview = FindViewById<ListView>(Resource.Id.pingListView);
			myview.Adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleListItem1, messages);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button>(Resource.Id.myButton);

			button.Click += delegate 
			{
				AlertDialog.Builder alert = new AlertDialog.Builder(this);
				BluetoothAdapter adapter = BluetoothAdapter.DefaultAdapter;
				List<string> descriptions = new List<string>();
				foreach (BluetoothDevice device in adapter.BondedDevices)
				{
					ParcelUuid[] uuids = device.GetUuids();
					for (int j = 0; j < uuids.Length; j++)
					{
						if (uuids[j].Uuid.ToString() == serialPortUUID.ToString())
						{
							descriptions.Add(device.Name + " - " + device.Address);
							// We wont this one
							//entries[i] = device.Name;
							//entryValues[i] = device.Address;
							//i++;
						}
					}

				}

				alert.SetTitle("Pick something, fool!");
				alert.SetItems(descriptions.ToArray(),
				async (sender, e) =>
				{
					string deviceDescription = descriptions[e.Which];
					string[] descripSplit = deviceDescription.Split(" - ".ToCharArray());
					string mac = descripSplit[descripSplit.Length - 1];
					byte[] macbytes = macStringToBytes(mac);
					if (macbytes != null)
					{
						device = BluetoothAdapter.DefaultAdapter.GetRemoteDevice(macbytes);
						button.Text = deviceDescription;
						socket = device.CreateRfcommSocketToServiceRecord(serialPortUUID);
						bool connected = false;
						if (socket != null)
						{
							SynchronizationContext currentContext = SynchronizationContext.Current;
							try
							{
								await Task.Run(async () =>
								{
									await socket.ConnectAsync();
									if (socket.IsConnected)
									{
										connected = true;
										System.IO.Stream stream = socket.InputStream;
										byte[] readbytes = new byte[16];
										int len = 16;

										List<byte> byteList = new List<byte>();
										while (socket.IsConnected)
										{
											int bytesread = stream.Read(readbytes, 0, len);
											if (bytesread > 0)
											{
												for (int i = 0; i < bytesread; i++)
												{
													byteList.Add(readbytes[i]);
												}
											}
											//forward to header
											while (byteList.Count > 0)
											{
												if (byteList[0] == (byte)'V')
												{
													break;
												}
												byteList.RemoveAt(0);
											}
											if (byteList.Count > 1)
											{
												if (byteList[1] != (byte)'W')
												{
													//if header's not right, move off V and the message is trashed
													byteList.RemoveAt(0);
												}
											}

											if(byteList.Count > 3)
											{
												byte msglen = byteList[2];
												if (msglen <= 16)
												{
													if (byteList.Count >= msglen + 2)
													{
														//valid?
														ushort canID = 0x0000;
														canID += byteList[4];
														canID <<= 8;
														canID += byteList[5];

														string message = "ID: " + canID.ToString("X3");
														message += " Message:";
														for (byte i = 0; i < byteList[2] - 6; i++)
														{
															message += " 0x" + byteList[6 + i].ToString("X2");
														}
														byteList.RemoveRange(0, msglen + 2);
														RunOnUiThread(() =>
														{
															(myview.Adapter as ArrayAdapter).Add(message);
															//myview.ScrollTo(0, myview.Bottom);
														});
													}
												}
												else
												{
													//bad message length, move off V and trash the message
													byteList.RemoveAt(0);
												}
											}

										}

									}
								});


							}
							catch //(Java.IO.IOException ex)
							{
								
							}
						}
					}
				});

				//alert.SetMessage("Lorem ipsum dolor sit amet, consectetuer adipiscing elit.");
				/*alert.SetPositiveButton("Delete", (senderAlert, args) =>
				{
					Toast.MakeText(this, "Deleted!", ToastLength.Short).Show();
				});*/

				alert.SetNegativeButton("Cancel", (senderAlert, args) =>
				{
					Toast.MakeText(this, "Cancelled!", ToastLength.Short).Show();
				});

				Dialog dialog = alert.Create();
				dialog.Show();
			};
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


