using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CANSniffer
{
	public class CANInterpreter
	{
		BluetoothSerialPort port;
		readonly ushort[] crc_16_table = {  0x0000, 0xCC01, 0xD801, 0x1400, 0xF001, 0x3C00, 0x2800, 0xE401,
									     	0xA001, 0x6C00, 0x7800, 0xB401, 0x5000, 0x9C01, 0x8801, 0x4400 };

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

		public async Task SendHeartbeat()
		{
			if (port != null)
			{
				while (port.IsConnected)
				{
					await port.WriteAsync(new byte[] { (byte)'V', (byte)'W', 0x04, 0x03, 0xc1, 0x42 }, 0, 6);
					await Task.Delay(1000);
				}
			}
		}

		public async Task SendNewFilter(byte filterNum, ushort newFilter)
		{
			ushort crc = getCRC(new byte[] { 0x07, 0x12, filterNum, (byte)(newFilter >> 8), (byte)(newFilter & 0x00FF) });
			await port.WriteAsync(new byte[] { (byte)'V', (byte)'W', 0x07, 0x12, filterNum, (byte)(newFilter >> 8), (byte)(newFilter & 0x00FF), (byte)(crc >> 8), (byte)(crc & 0x00FF) }, 0, 9);
		}

		public async Task SendNewMask(byte maskNum, ushort newMask)
		{
			ushort crc = getCRC(new byte[] { 0x07, 0x12, maskNum, (byte)(newMask >> 8), (byte)(newMask & 0x00FF) });
			await port.WriteAsync(new byte[] { (byte)'V', (byte)'W', 0x07, 0x12, maskNum, (byte)(newMask >> 8), (byte)(newMask & 0x00FF), (byte)(crc >> 8), (byte)(crc & 0x00FF) }, 0, 9);
		}

		private async Task SendReadFilter(byte filterNum)
		{
			ushort crc = getCRC(new byte[] { 0x05, 0x02, filterNum });
			await port.WriteAsync(new byte[] { (byte)'V', (byte)'W', 0x05, 0x02, filterNum, (byte)(crc >> 8), (byte)(crc & 0x00FF) }, 0, 7);
		}

		private async Task SendReadMask(byte maskNum)
		{
			ushort crc = getCRC(new byte[] { 0x05, 0x01, maskNum });
			await port.WriteAsync(new byte[] { (byte)'V', (byte)'W', 0x05, 0x01, maskNum, (byte)(crc >> 8), (byte)(crc & 0x00FF) }, 0, 7);
		}

		public async Task InterpretStream()
		{
			if (port != null)
			{
				await SendReadMask(0);
				await SendReadMask(1);
				for (byte i = 0; i < 6; i++)
				{
					await SendReadFilter(i);
				}
				byte[] readbytes = new byte[16];
				int len = 1;//16;

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
									if (getCRC(byteList.GetRange(2, byteList[2] - 2).ToArray()) == readUShort(byteList, 2 + msglen - 2))//headerlen + msglen - crclen
									{
										switch (byteList[3])
										{
											case 0x00:
												//received CAN message
												if (CANMessageReceived != null)
												{
													ushort canID = readUShort(byteList, 4);
													CANMessageReceived(this, new CANMessageReceivedEventArgs(canID, byteList.GetRange(6, msglen - 6).ToArray()));
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
												}
												break;
											default:
												//unknown type
												break;
										}
									}
									else 
									{ }
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
					catch 
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

		private ushort getCRC(byte[] bytes)
		{
			ushort crc = 0x0000;
			for (int i = 0; i < bytes.Length; i++)
			{
				ushort r = crc_16_table[crc & 0x000F];
				crc = (ushort)((crc >> 4) & 0x0FFF);
				crc = (ushort)(crc ^ r ^ crc_16_table[bytes[i] & 0x0F]);
				/* now compute checksum of upper four bits of ucData */
				r = crc_16_table[crc & 0x000F];
				crc = (ushort)((crc >> 4) & 0x0FFF);
				crc = (ushort)(crc ^ r ^ crc_16_table[(bytes[i] >> 4) & 0x0F]);
			}
			return crc;
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

