using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.OS;

namespace CANSniffer
{
	public class BluetoothSerialPort
	{
		BluetoothDevice device;
		BluetoothSocket socket;
		System.IO.Stream istream;
		System.IO.Stream ostream;
		//bool connected = false;

		static Java.Util.UUID serialPortUUID = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");



		BluetoothSerialPort()
		{
		}

		public BluetoothSerialPort(byte[] macbytes) : this()
		{
			device = BluetoothAdapter.DefaultAdapter.GetRemoteDevice(macbytes);
			socket = device.CreateRfcommSocketToServiceRecord(serialPortUUID);
		}

		static public ICollection<BluetoothDevice> BondedSerialDevices
		{
			get
			{
				List<BluetoothDevice> devices = new List<BluetoothDevice>();
				foreach (BluetoothDevice device in BluetoothAdapter.DefaultAdapter.BondedDevices)
				{
					ParcelUuid[] uuids = device.GetUuids();
					for (int j = 0; j < uuids.Length; j++)
					{
						if (uuids[j].Uuid.ToString() == serialPortUUID.ToString())
						{

							devices.Add(device);
						}
					}

				}
				return devices;
			}
		}

		public bool IsConnected
		{
			get
			{
				//return connected;
				return (socket != null ? socket.IsConnected : false);
			}
			/*protected set
			{
				connected = value;
			}*/
		}

		public void Connect()
		{
			if (socket != null)
			{
				try
				{
					socket.Connect();

					if (IsConnected)
					{
						istream = socket.InputStream;
						ostream = socket.OutputStream;
					}
					//raise event
					SynchronizationContext.Current.Post((e) =>
					{
						if (BluetoothConnectionReceived != null)
							BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected));
					}, null);
				}
				catch (Exception ex)
				{
					//raise event
					SynchronizationContext.Current.Post((e) =>
					{
						if (BluetoothConnectionReceived != null)
							BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected, ex));
					}, null);
				}
			}
		}

		public async Task ConnectAsync()
		{
			await Task.Run(async () =>
			{
				try
				{
					await socket.ConnectAsync();
					if (IsConnected)
					{
						istream = socket.InputStream;
						ostream = socket.OutputStream;
					}
					//raise event
					//SynchronizationContext.Current.Post((e) =>
					{
						if (BluetoothConnectionReceived != null)
							BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected));
					}//, null);
				}
				catch (Exception ex)
				{
					//raise event
					//SynchronizationContext.Current.Post((e) =>
					{
						if (BluetoothConnectionReceived != null)
							BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected, ex));
					}//, null);
				}
			});
		}

		public event EventHandler<BluetoothConnectionReceivedEventArgs> BluetoothConnectionReceived;

		public int Read(byte[] buffer, int offset, int count)
		{
			if (IsConnected)
			{
				return istream.Read(buffer, offset, count);
			}
			return 0;
		}
	}


	public class BluetoothConnectionReceivedEventArgs : EventArgs
	{
		public BluetoothConnectionReceivedEventArgs(string address, string name, bool connected, Exception ex = null)
		{
			Address = address;
			Name = name;
			Connected = connected;
			Ex = ex;
		}

		string _address;
		public string Address
		{
			get
			{
				return _address;
			}
			protected set
			{
				_address = value;
			}
		}

		string _name;
		public string Name
		{
			get
			{
				return _name;
			}
			protected set
			{
				_name = value;
			}
		}

		bool _connected;
		public bool Connected
		{
			get
			{
				return _connected;
			}
			protected set
			{
				_connected = value;
			}
		}

		Exception _ex;
		public Exception Ex
		{
			get
			{
				return _ex;
			}
			protected set
			{
				_ex = value;
			}
		}
	}
}

