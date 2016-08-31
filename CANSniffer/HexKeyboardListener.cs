using System;
using Android.App;
using Android.InputMethodServices;
using Android.Views;
using Java.Lang;

namespace CANSniffer
{
	public class HexKeyboardListener : Java.Lang.Object, KeyboardView.IOnKeyboardActionListener
	{
		private readonly Activity _activity;

		public HexKeyboardListener(Activity activity)
		{
			_activity = activity;
		}

		public void OnKey(Android.Views.Keycode primaryCode, Android.Views.Keycode[] keyCodes)
		{
			
			var eventTime = DateTime.Now.Ticks;
			var keyEvent = new KeyEvent(eventTime, eventTime, KeyEventActions.Down, primaryCode, 0);
			_activity.DispatchKeyEvent(keyEvent);
		}

		public void OnPress(Android.Views.Keycode primaryCode) { }
		public void OnRelease(Android.Views.Keycode primaryCode) { }
		public void OnText(ICharSequence text) { }
		public void SwipeDown() { }
		public void SwipeLeft() { }
		public void SwipeRight() { }
		public void SwipeUp() { }
	}

	public class HexKeyboardOnFocusChangeListener : Java.Lang.Object, View.IOnFocusChangeListener
	{
		private readonly Activity _activity;

		private string undo = null;

		public HexKeyboardOnFocusChangeListener(Activity activity)
		{
			_activity = activity;
		}

		public void OnFocusChange(View v, bool hasFocus)
		{
			if (hasFocus)
			{
				undo = (v as Android.Widget.EditText).Text;
				(_activity as MainActivity).ShowHexKeyboard(v);
			}
			else
			{
				if (undo != null)
				{
					(v as Android.Widget.EditText).Text = undo;
				}
				(_activity as MainActivity).HideHexKeyboard();
			}
		}

		public void Set()
		{
			undo = null;
		}
	}

	public class HexKeyboardOnClickListener : Java.Lang.Object, View.IOnClickListener
	{
		private readonly Activity _activity;

		public HexKeyboardOnClickListener(Activity activity)
		{
			_activity = activity;
		}

		public void OnClick(View v)
		{
			(_activity as MainActivity).ShowHexKeyboard(v);
		}
	}
}

