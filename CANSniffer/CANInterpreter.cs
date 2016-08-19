using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CANSniffer
{
	public class CANInterpreter
	{
		BluetoothSerialPort port;

		public CANInterpreter()
		{
		}

		public BluetoothSerialPort Port
		{
			get
			{
				return port;
			}
			set
			{
				port = value;
			}
		}

		public event EventHandler<CANMessageReceivedEventArgs> CANMessageReceived;

		public async Task InterpretStream()
		{
			await (Task.Run(() =>
			{
				if (port != null)
				{
					byte[] readbytes = new byte[16];
					int len = 16;

					List<byte> byteList = new List<byte>();
					while (port.IsConnected)
					{
						int bytesread = port.Read(readbytes, 0, len);
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

						if (byteList.Count > 3)
						{
							byte msglen = byteList[2];
							if (msglen <= 16)
							{
								switch (byteList[3])
								{
									case 0x00:
										if (byteList.Count >= msglen + 2)
										{
											if (CANMessageReceived != null)
											{
												ushort canID = 0x0000;
												canID += byteList[4];
												canID <<= 8;
												canID += byteList[5];
												CANMessageReceived(this, new CANMessageReceivedEventArgs(canID, byteList.GetRange(6, msglen - 6).ToArray()));
											}
											byteList.RemoveRange(0, msglen + 2);
										}
										break;
									default:
										//unknown type, move off V & trash
										byteList.RemoveAt(0);
										break;
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
			}));
		}
	}

	public class CANMessageReceivedEventArgs : EventArgs
	{
		public CANMessageReceivedEventArgs(ushort canID, byte[] message)
		{
			CANID = canID;
			Message = message;
		}

		ushort _canID;
		public ushort CANID
		{
			get
			{
				return _canID;
			}
			protected set
			{
				_canID = value;
			}
		}

		byte[] _buffer;
		public byte[] Message
		{
			get
			{
				return _buffer;
			}
			protected set
			{
				_buffer = value;
			}
		}
	}
}

