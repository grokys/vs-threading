﻿namespace Microsoft.VisualStudio.Threading {
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// An asynchronous implementation of an AutoResetEvent.
	/// </summary>
	[DebuggerDisplay("Signaled: {signaled}")]
	public class AsyncAutoResetEvent {
		/// <summary>
		/// A queue of folks awaiting signals.
		/// </summary>
		private readonly Queue<TaskCompletionSource<bool>> signalAwaiters = new Queue<TaskCompletionSource<bool>>();

		/// <summary>
		/// Whether to complete the task synchronously in the <see cref="Set"/> method,
		/// as opposed to asynchronously.
		/// </summary>
		private readonly bool allowInliningAwaiters;

		/// <summary>
		/// A value indicating whether this event is already in a signaled state.
		/// </summary>
		private bool signaled;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class
		/// that does not inline awaiters.
		/// </summary>
		public AsyncAutoResetEvent() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class.
		/// </summary>
		/// <param name="allowInliningAwaiters">
		/// A value indicating whether to complete the task synchronously in the <see cref="Set"/> method,
		/// as opposed to asynchronously. <c>false</c> better simulates the behavior of the
		/// <see cref="AutoResetEvent"/> class, but <c>true</c> can result in slightly better performance.
		/// </param>
		public AsyncAutoResetEvent(bool allowInliningAwaiters) {
			this.allowInliningAwaiters = allowInliningAwaiters;
		}

		/// <summary>
		/// Returns an awaitable that may be used to asynchronously acquire the next signal.
		/// </summary>
		/// <returns>An awaitable.</returns>
		public Task WaitAsync() {
			lock (this.signalAwaiters) {
				if (this.signaled) {
					this.signaled = false;
					return TplExtensions.CompletedTask;
				} else {
					var tcs = new TaskCompletionSource<bool>();
					this.signalAwaiters.Enqueue(tcs);
					return tcs.Task;
				}
			}
		}

		/// <summary>
		/// Sets the signal if it has not already been set, allowing one awaiter to handle the signal if one is already waiting.
		/// </summary>
		public void Set() {
			TaskCompletionSource<bool> toRelease = null;
			lock (this.signalAwaiters) {
				if (this.signalAwaiters.Count > 0) {
					toRelease = this.signalAwaiters.Dequeue();
				} else if (!this.signaled) {
					this.signaled = true;
				}
			}

			if (toRelease != null) {
				if (this.allowInliningAwaiters) {
					toRelease.SetResult(true);
				} else {
					ThreadPool.QueueUserWorkItem(state => ((TaskCompletionSource<bool>)state).SetResult(true), toRelease);
				}
			}
		}
	}
}