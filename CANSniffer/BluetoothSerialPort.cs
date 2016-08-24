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
			istream = socket.InputStream;
			ostream = socket.OutputStream;
		}

		public BluetoothSerialPort(string mac) : this()
		{
			
			device = BluetoothAdapter.DefaultAdapter.GetRemoteDevice(macStringToBytes(mac));
			socket = device.CreateRfcommSocketToServiceRecord(serialPortUUID);
			istream = socket.InputStream;
			ostream = socket.OutputStream;
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

		public string Address
		{
			get
			{
				if (device != null)
				{
					return device.Address;
				}
				return null;
			}
		}

		public string Name
		{
			get
			{
				if (device != null)
				{
					return device.Name;
				}
				return null;
			}
		}

		public event EventHandler<BluetoothConnectionReceivedEventArgs> BluetoothConnectionReceived;

		public void Connect()
		{
			if (socket != null)
			{
				try
				{
					socket.Connect();
					//raise event
					if (BluetoothConnectionReceived != null)
					{
						BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected));
					}
				}
				catch (Exception ex)
				{
					//raise event
					if (BluetoothConnectionReceived != null)
					{
						BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected, ex));
					}
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
					//raise event
					if (BluetoothConnectionReceived != null)
					{
						BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected));
					}
				}
				catch (Exception ex)
				{
					//raise event
					if (BluetoothConnectionReceived != null)
					{
						BluetoothConnectionReceived(this, new BluetoothConnectionReceivedEventArgs(device.Address, device.Name, IsConnected, ex));
					}
				}
			});
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			return (IsConnected ? istream.Read(buffer, offset, count) : -1);
		}

		public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
		{
			return (IsConnected ? await istream.ReadAsync(buffer, offset, count) : -1);
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			if (IsConnected)
			{
				ostream.Write(buffer, offset, count);
			}
		}

		public async Task WriteAync(byte[] buffer, int offset, int count)
		{
			if (IsConnected)
			{
				await ostream.WriteAsync(buffer, offset, count);
			}
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

