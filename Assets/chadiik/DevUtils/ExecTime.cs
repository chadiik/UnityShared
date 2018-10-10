﻿using UnityEngine;
using System;
using Stopwatch = System.Diagnostics.Stopwatch;

// comment from symlink on some project

namespace chadiik.devUtils {
	/// <summary>
	/// Helper class to cleanly check execution times of a delegate function
	/// I personally leave most of those in builds; the penalty is the creation of a string literal,
	/// and execution of 2 addional functions, but not creating a new ExecTimeObject or Stopwatch.
	/// </summary>
	public class ExecTime {

#if UNITY_EDITOR
		private class ExecTimeObject<T> {

			public string title;

			public ExecTimeObject ( string title ) {

				this.title = title;
			}

			public T Run ( Func<T> func ) {

				Stopwatch watch = Stopwatch.StartNew();
				T result = func();
				watch.Stop ();
				Debug.LogFormat ( "{0} ({1} ms)", title, watch.ElapsedMilliseconds.ToString ( "0.00" ) );
				return result;
			}

			public void Run ( Action action ) {

				Stopwatch watch = Stopwatch.StartNew();
				action ();
				watch.Stop ();
				Debug.LogFormat ( "{0} ({1} ms)", title, watch.ElapsedMilliseconds.ToString ( "0.00" ) );
			}
		}

#endif

		public static T Of<T> ( string title, Func<T> func ) {
#if UNITY_EDITOR
			return new ExecTimeObject<T> ( title ).Run ( func );
#else
		return func();
#endif
		}

		public static void Of ( string title, Action action ) {
#if UNITY_EDITOR
			new ExecTimeObject<object> ( title ).Run ( action );
#else
		action();
#endif
		}
	}
}