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
		public event EventHandler<CANSettingReceivedEventArgs> CANMaskSettingReceived;
		public event EventHandler<CANSettingReceivedEventArgs> CANFilterSettingReceived;


		public async Task InterpretStream()
		{
			if (port != null)
			{
				byte[] readbytes = new byte[16];
				int len = 16;

				List<byte> byteList = new List<byte>();
				while (port.IsConnected)
				{
					try
					{
						int bytesread = await port.ReadAsync(readbytes, 0, len);
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
								continue;
							}
						}

						if (byteList.Count > 3)
						{
							byte msglen = byteList[2];
							if (msglen <= 16)
							{
								if (byteList.Count >= msglen + 2)
								{
									ushort crc = readUShort(byteList, 2 + msglen - 2);//headerlen + msglen - crclen
									switch (byteList[3])
									{
										case 0x00:
											//received CAN message
											if (CANMessageReceived != null)
											{
												ushort canID = readUShort(byteList, 4);
												CANMessageReceived(this, new CANMessageReceivedEventArgs(canID, byteList.GetRange(5, msglen - 5).ToArray()));
											}
											break;
										case 0x01:
											//get/set bitmask
											if (CANMaskSettingReceived != null)
											{
												ushort setting = readUShort(byteList, 5);
												CANMaskSettingReceived(this, new CANSettingReceivedEventArgs(byteList[4], setting));
											}
											break;
										case 0x02:
											//get/set filter
											if (byteList.Count >= msglen + 2)
											{
												if (CANFilterSettingReceived != null)
												{
													ushort setting = readUShort(byteList, 5);
													CANFilterSettingReceived(this, new CANSettingReceivedEventArgs(byteList[4], setting));
												}
												byteList.RemoveRange(0, msglen + 2);
											}
											break;
										default:
											//unknown type
											break;
									}
									byteList.RemoveRange(0, msglen + 2);
								}

							}
							else
							{
								//bad message length, move off V and trash the message
								byteList.RemoveAt(0);
							}
						}
					}
					catch(Exception ex)
					{ }
				}
			}
		}

		private ushort readUShort(List<byte> bytes, int start)
		{
			ushort result = 0x000;
			try
			{
				result = (ushort)(((ushort)bytes[start]) << 8);
				result += bytes[start + 1];
			}
			catch { }
			return result;
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

	public class CANSettingReceivedEventArgs : EventArgs
	{
		public CANSettingReceivedEventArgs(byte index, ushort setting)
		{
			Index = index;
			Setting = setting;
		}

		byte _index;
		public byte Index
		{
			get
			{
				return _index;
			}
			protected set
			{
				_index = value;
			}
		}

		ushort _setting;
		public ushort Setting
		{
			get
			{
				return _setting;
			}
			protected set
			{
				_setting = value;
			}
		}
	}
}

