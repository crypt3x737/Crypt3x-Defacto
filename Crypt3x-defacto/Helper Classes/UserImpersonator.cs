using Microsoft.Win32.SafeHandles;
using System;
using System.DirectoryServices.AccountManagement;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;

namespace Helper {
	[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
	public class UserImpersonator {
		// This class was modified from code found here https://msdn.microsoft.com/en-us/library/w070t6ka(v=vs.110).aspx

		private sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid {
			private SafeTokenHandle() : base(true) { }

			[DllImport("kernel32.dll")]
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			[SuppressUnmanagedCodeSecurity]
			[return: MarshalAs(UnmanagedType.Bool)]
			private static extern bool CloseHandle(IntPtr handle);

			protected override bool ReleaseHandle() {
				return CloseHandle(handle);
			}
		}

		// Some constants for LogonUser().
		private static class Logon32 {
			public enum Logon {
				// ???           = 0,
				// ???           = 1,
				Interactive      = 2,
				Network          = 3,
				Batch            = 4,
				Service          = 5,
				// ???           = 6,
				Unlock           = 7,
				NetworkCleartext = 8, // WINNT50 only
				NewCredentials   = 9  // WINNT50 only
			}
			public enum Provider {
				Default = 0,
				// ???  = 1,
				WinNT40 = 2,
				WinNT50 = 3
			}
		}

		// https://msdn.microsoft.com/en-us/library/windows/desktop/aa378184(v=vs.85).aspx
		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

		public readonly string name, domain;
		public readonly SecureString password;

		SafeTokenHandle safeTokenHandle;
		WindowsImpersonationContext winIC;

		// The local machine name can be used for the domain name to impersonate a user on this machine.
		public UserImpersonator(string n, string d, SecureString p) {
			name = n;
			domain = d;
			password = p;
		}

		public bool start() {
			try {
				if (winIC != null || safeTokenHandle != null)
					return false;

				// Convert SecureString password to string insecure_pass.
				var insecure_pass = new System.Net.NetworkCredential("", password).Password;

				// Check if the username and password work on the given domain.
				using (var pc = new PrincipalContext(ContextType.Domain, domain))
					if (!pc.ValidateCredentials(name, insecure_pass))
						throw new System.Security.Authentication.AuthenticationException("The user name or password is incorrect.");

				// Call LogonUser to obtain a handle to an access token.
				var logonSuccess = LogonUser(name, domain, insecure_pass, (int)Logon32.Logon.NewCredentials, (int)Logon32.Provider.WinNT50, out safeTokenHandle);

				if (!logonSuccess)
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

				// Use the token handle returned by LogonUser.
				winIC = WindowsIdentity.Impersonate(safeTokenHandle.DangerousGetHandle());

				return true;
			} catch (Exception ex) {
				stop();
				System.Windows.Forms.MessageBox.Show(ex.Message);
				return false;
			}
		}

		public void stop() {
			if (winIC != null) {
				winIC.Undo();
				winIC.Dispose();
				winIC = null;
			}

			if (safeTokenHandle != null) {
				safeTokenHandle.Dispose();
				safeTokenHandle = null;
			}
		}
	}
}
