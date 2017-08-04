using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;

namespace Pri.LongPath
{
	using FileNotFoundException = System.IO.FileNotFoundException;

	public class Common
	{
	    public static bool IsRunningOnMono()
	    {
	       return Type.GetType("Mono.Runtime") != null;
	    }

	    private static readonly uint ProtectedDiscretionaryAcl = 0x80000000;
		private static readonly uint ProtectedSystemAcl = 0x40000000;
		private static readonly uint UnprotectedDiscretionaryAcl = 0x20000000;
		private static readonly uint UnprotectedSystemAcl = 0x10000000;

		internal static void SetAttributes(string path, FileAttributes fileAttributes)
		{
			string normalizedPath = Path.NormalizeLongPath(path);
			if (!NativeMethods.SetFileAttributes(normalizedPath, fileAttributes))
			{
				throw GetExceptionFromLastWin32Error();
			}
		}

		internal static FileAttributes GetAttributes(string path)
		{
			string normalizedPath = Path.NormalizeLongPath(path);
			FileAttributes fileAttributes;

			int errorCode = TryGetDirectoryAttributes(normalizedPath, out fileAttributes);
			if (errorCode != NativeMethods.ERROR_SUCCESS) throw GetExceptionFromWin32Error(errorCode);

			return fileAttributes;
		}

		internal static FileAttributes GetAttributes(string path, out int errorCode)
		{
			string normalizedPath = Path.NormalizeLongPath(path);
			FileAttributes fileAttributes = (FileAttributes)(NativeMethods.INVALID_FILE_ATTRIBUTES);

			errorCode = TryGetDirectoryAttributes(normalizedPath, out fileAttributes);

			return fileAttributes;
		}

		internal static FileAttributes GetFileAttributes(string path)
		{
			string normalizedPath = Path.NormalizeLongPath(path);
			FileAttributes fileAttributes;

			int errorCode = TryGetFileAttributes(normalizedPath, out fileAttributes);
			if (errorCode != NativeMethods.ERROR_SUCCESS) throw GetExceptionFromWin32Error(errorCode);

			return fileAttributes;
		}

		internal static string NormalizeSearchPattern(string searchPattern)
		{
			if (String.IsNullOrEmpty(searchPattern) || searchPattern == ".")
				return "*";

			return searchPattern;
		}

		internal static bool Exists(string path, out bool isDirectory)
		{
			string normalizedPath = path;
			if (Path.TryNormalizeLongPath(path, out normalizedPath) || IsPathUnc(path))
			{
				FileAttributes attributes;
				int errorCode = TryGetFileAttributes(normalizedPath, out attributes);
				if (errorCode == 0 && (int)attributes != NativeMethods.INVALID_FILE_ATTRIBUTES)
				{
					isDirectory = Directory.IsDirectory(attributes);
					return true;
				}
			}

			isDirectory = false;
			return false;
		}

		internal static int TryGetDirectoryAttributes(string normalizedPath, out FileAttributes attributes)
		{
			int errorCode = TryGetFileAttributes(normalizedPath, out attributes);

			return errorCode;
		}

		internal static int TryGetFileAttributes(string normalizedPath, out FileAttributes attributes)
		{
			NativeMethods.WIN32_FILE_ATTRIBUTE_DATA data = new NativeMethods.WIN32_FILE_ATTRIBUTE_DATA();

			int errorMode = NativeMethods.SetErrorMode(1);
			bool success = false;
			try
			{
				success = NativeMethods.GetFileAttributesEx(normalizedPath, 0, ref data);

			}
			finally
			{
				NativeMethods.SetErrorMode(errorMode);
			}

			if (!success)
			{
				attributes = (FileAttributes)NativeMethods.INVALID_FILE_ATTRIBUTES;
				return Marshal.GetLastWin32Error();
			}

			attributes = data.fileAttributes;
			return 0;

			//// NOTE: Don't be tempted to use FindFirstFile here, it does not work with root directories

			//attributes = NativeMethods.GetFileAttributes(normalizedPath);
			//if ((int)attributes == NativeMethods.INVALID_FILE_ATTRIBUTES)
			//	return Marshal.GetLastWin32Error();

			//return 0;
		}

		internal static Exception GetExceptionFromLastWin32Error()
		{
			return GetExceptionFromLastWin32Error("path");
		}

		internal static Exception GetExceptionFromLastWin32Error(string parameterName)
		{
			return GetExceptionFromWin32Error(Marshal.GetLastWin32Error(), parameterName);
		}

		internal static Exception GetExceptionFromWin32Error(int errorCode)
		{
			return GetExceptionFromWin32Error(errorCode, "path");
		}

		internal static Exception GetExceptionFromWin32Error(int errorCode, string parameterName)
		{
			string message = GetMessageFromErrorCode(errorCode);

			switch (errorCode)
			{
				case NativeMethods.ERROR_FILE_NOT_FOUND:
					return new FileNotFoundException(message);

				case NativeMethods.ERROR_PATH_NOT_FOUND:
					return new DirectoryNotFoundException(message);

				case NativeMethods.ERROR_ACCESS_DENIED:
					return new UnauthorizedAccessException(message);

				case NativeMethods.ERROR_FILENAME_EXCED_RANGE:
					return new PathTooLongException(message);

				case NativeMethods.ERROR_INVALID_DRIVE:
					return new DriveNotFoundException(message);

				case NativeMethods.ERROR_OPERATION_ABORTED:
					return new OperationCanceledException(message);

				case NativeMethods.ERROR_INVALID_NAME:
					return new ArgumentException(message, parameterName);

				default:
					return new IOException(message, NativeMethods.MakeHRFromErrorCode(errorCode));

			}
		}

		private static string GetMessageFromErrorCode(int errorCode)
		{
			StringBuilder buffer = new StringBuilder(512);

			/*int bufferLength = */NativeMethods.FormatMessage(NativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS | NativeMethods.FORMAT_MESSAGE_FROM_SYSTEM | NativeMethods.FORMAT_MESSAGE_ARGUMENT_ARRAY, IntPtr.Zero, errorCode, 0, buffer, buffer.Capacity, IntPtr.Zero);

			//Contract.Assert(bufferLength != 0);

			return buffer.ToString();
		}

		internal static void ThrowIOError(int errorCode, String maybeFullPath)
		{
			// This doesn't have to be perfect, but is a perf optimization.
			bool isInvalidPath = errorCode == NativeMethods.ERROR_INVALID_NAME || errorCode == NativeMethods.ERROR_BAD_PATHNAME;
			String str = isInvalidPath ? Path.GetFileName(maybeFullPath) : maybeFullPath;

			switch (errorCode)
			{
				case NativeMethods.ERROR_FILE_NOT_FOUND:
					if (str.Length == 0)
						throw new FileNotFoundException("Empty filename");
					else
						throw new FileNotFoundException(String.Format("File {0} not found", str), str);

				case NativeMethods.ERROR_PATH_NOT_FOUND:
					if (str.Length == 0)
						throw new DirectoryNotFoundException("Empty directory");
					else
						throw new DirectoryNotFoundException(String.Format("Directory {0} not found", str));

				case NativeMethods.ERROR_ACCESS_DENIED:
					if (str.Length == 0)
						throw new UnauthorizedAccessException("Empty path");
					else
						throw new UnauthorizedAccessException(String.Format("Access denied accessing {0}", str));

				case NativeMethods.ERROR_ALREADY_EXISTS:
					if (str.Length == 0)
						goto default;
					throw new IOException(String.Format("File {0}", str), NativeMethods.MakeHRFromErrorCode(errorCode));

				case NativeMethods.ERROR_FILENAME_EXCED_RANGE:
					throw new PathTooLongException("Path too long");

				case NativeMethods.ERROR_INVALID_DRIVE:
					throw new DriveNotFoundException(String.Format("Drive {0} not found", str));

				case NativeMethods.ERROR_INVALID_PARAMETER:
					throw new IOException(NativeMethods.GetMessage(errorCode), NativeMethods.MakeHRFromErrorCode(errorCode));

				case NativeMethods.ERROR_SHARING_VIOLATION:
					if (str.Length == 0)
						throw new IOException("Sharing violation with empty filename", NativeMethods.MakeHRFromErrorCode(errorCode));
					else
						throw new IOException(String.Format("Sharing violation: {0}", str), NativeMethods.MakeHRFromErrorCode(errorCode));

				case NativeMethods.ERROR_FILE_EXISTS:
					if (str.Length == 0)
						goto default;
					throw new IOException(String.Format("File exists {0}", str), NativeMethods.MakeHRFromErrorCode(errorCode));

				case NativeMethods.ERROR_OPERATION_ABORTED:
					throw new OperationCanceledException();

				default:
					throw new IOException(NativeMethods.GetMessage(errorCode), NativeMethods.MakeHRFromErrorCode(errorCode));
			}
		}

		internal static void ThrowIfError(int errorCode, IntPtr byteArray)
		{
			if (errorCode == NativeMethods.ERROR_SUCCESS)
			{
				if (IntPtr.Zero.Equals(byteArray))
				{
					//
					// This means that the object doesn't have a security descriptor. And thus we throw
					// a specific exception for the caller to catch and handle properly.
					//
					throw new InvalidOperationException("Object does not have security descriptor,");
				}
			}
			else
			{
				switch (errorCode)
				{
					case NativeMethods.ERROR_NOT_ALL_ASSIGNED:
					case NativeMethods.ERROR_PRIVILEGE_NOT_HELD:

#if netfx
						throw new PrivilegeNotHeldException("SeSecurityPrivilege");
#else
                        throw new Exception();
#endif
                    case NativeMethods.ERROR_ACCESS_DENIED:
					case NativeMethods.ERROR_CANT_OPEN_ANONYMOUS:
					case NativeMethods.ERROR_LOGON_FAILURE:
						throw new UnauthorizedAccessException();
					case NativeMethods.ERROR_NOT_ENOUGH_MEMORY:
						throw new OutOfMemoryException();
					case NativeMethods.ERROR_BAD_NETPATH:
					case NativeMethods.ERROR_NETNAME_DELETED:
					default:
						throw new IOException(NativeMethods.GetMessage(errorCode), NativeMethods.MakeHRFromErrorCode(errorCode));
				}
			}
		}

#if netfx
		internal static SecurityInfos ToSecurityInfos(AccessControlSections accessControlSections)
		{
			SecurityInfos securityInfos = 0;

			if ((accessControlSections & AccessControlSections.Owner) != 0)
			{
				securityInfos |= SecurityInfos.Owner;
			}

			if ((accessControlSections & AccessControlSections.Group) != 0)
			{
				securityInfos |= SecurityInfos.Group;
			}

			if ((accessControlSections & AccessControlSections.Access) != 0)
			{
				securityInfos |= SecurityInfos.DiscretionaryAcl;
			}

			if ((accessControlSections & AccessControlSections.Audit) != 0)
			{
				securityInfos |= SecurityInfos.SystemAcl;
				//privilege = new Privilege(Privilege.Security);
			}

			return securityInfos;
		}

		internal static void SetAccessControlExtracted(FileSystemSecurity security, string name)
		{
			//security.WriteLock();
			AccessControlSections includeSections = AccessControlSections.Owner | AccessControlSections.Group;
			if(security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Count > 0)
			{
				includeSections |= AccessControlSections.Access;
			}
			if (security.GetAuditRules(true, false, typeof(SecurityIdentifier)).Count > 0)
			{
				includeSections |= AccessControlSections.Audit;
			}
			
			SecurityInfos securityInfo = (SecurityInfos)0;
			SecurityIdentifier owner = null;
			SecurityIdentifier group = null;
			SystemAcl sacl = null;
			DiscretionaryAcl dacl = null;
			if ((includeSections & AccessControlSections.Owner) != AccessControlSections.None)
			{
				owner = (SecurityIdentifier)security.GetOwner(typeof(SecurityIdentifier));
				if (owner != null)
				{
					securityInfo = securityInfo | SecurityInfos.Owner;
				}
			}

			if ((includeSections & AccessControlSections.Group) != AccessControlSections.None)
			{
				@group = (SecurityIdentifier)security.GetGroup(typeof(SecurityIdentifier));
				if (@group != null)
				{
					securityInfo = securityInfo | SecurityInfos.Group;
				}
			}
			var securityDescriptorBinaryForm = security.GetSecurityDescriptorBinaryForm();
			RawSecurityDescriptor rawSecurityDescriptor = null;
			bool isDiscretionaryAclPresent = false;
			if (securityDescriptorBinaryForm != null)
			{
				rawSecurityDescriptor = new RawSecurityDescriptor(securityDescriptorBinaryForm, 0);
				isDiscretionaryAclPresent = (rawSecurityDescriptor.ControlFlags & ControlFlags.DiscretionaryAclPresent) != ControlFlags.None;
			}

			if ((includeSections & AccessControlSections.Audit) != AccessControlSections.None)
			{
				securityInfo = securityInfo | SecurityInfos.SystemAcl;
				sacl = null;
				if (rawSecurityDescriptor != null)
				{
					var isSystemAclPresent = (rawSecurityDescriptor.ControlFlags & ControlFlags.SystemAclPresent) != ControlFlags.None;
					if (isSystemAclPresent && rawSecurityDescriptor.SystemAcl != null && rawSecurityDescriptor.SystemAcl.Count > 0)
					{
						// are all system acls on a file not a container?
						const bool notAContainer = false;
						const bool notADirectoryObjectACL = false;

						sacl = new SystemAcl(notAContainer, notADirectoryObjectACL,
							rawSecurityDescriptor.SystemAcl);
					}
					securityInfo = (SecurityInfos)(((rawSecurityDescriptor.ControlFlags & ControlFlags.SystemAclProtected) == ControlFlags.None ?
						(uint)securityInfo | UnprotectedSystemAcl : (uint)securityInfo | ProtectedSystemAcl));
				}
			}
			if ((includeSections & AccessControlSections.Access) != AccessControlSections.None && isDiscretionaryAclPresent)
			{
				securityInfo = securityInfo | SecurityInfos.DiscretionaryAcl;
				dacl = null;
				if (rawSecurityDescriptor != null)
				{
					//if (!this._securityDescriptor.DiscretionaryAcl.EveryOneFullAccessForNullDacl)
					{
						dacl = new DiscretionaryAcl(false, false, rawSecurityDescriptor.DiscretionaryAcl);
					}
					securityInfo = (SecurityInfos)(((rawSecurityDescriptor.ControlFlags & ControlFlags.DiscretionaryAclProtected) == ControlFlags.None ?
						(uint)securityInfo | UnprotectedDiscretionaryAcl : (uint)securityInfo | ProtectedDiscretionaryAcl));
				}
			}
			if (securityInfo == 0) return;

			int errorNum = SetSecurityInfo(ResourceType.FileObject, name, null, securityInfo, owner, @group, sacl, dacl);
			if (errorNum != 0)
			{
				Exception exception = GetExceptionFromWin32Error(errorNum, name);
				if (exception == null)
				{
					if (errorNum == NativeMethods.ERROR_ACCESS_DENIED)
					{
						exception = new UnauthorizedAccessException();
					}
					else if (errorNum == NativeMethods.ERROR_INVALID_OWNER)
					{
						exception = new InvalidOperationException("Invalid owner");
					}
					else if (errorNum == NativeMethods.ERROR_INVALID_PRIMARY_GROUP)
					{
						exception = new InvalidOperationException("Invalid group");
					}
					else if (errorNum == NativeMethods.ERROR_INVALID_NAME)
					{
						exception = new ArgumentException("Invalid name", "name");
					}
					else if (errorNum == NativeMethods.ERROR_INVALID_HANDLE)
					{
						exception = new NotSupportedException("Invalid Handle");
					}
					else if (errorNum == NativeMethods.ERROR_FILE_NOT_FOUND)
					{
						exception = new FileNotFoundException();
					}
					else if (errorNum != NativeMethods.ERROR_NO_SECURITY_ON_OBJECT)
					{
						exception = new InvalidOperationException("Unexpected error");
					}
					else
					{
						exception = new NotSupportedException("No associated security");
					}
				}
				throw exception;
			}
			//finally
			//{
			//security.WriteLUnlck();
			//}
		}

		internal static int SetSecurityInfo(
					ResourceType type,
					string name,
					SafeHandle handle,
					SecurityInfos securityInformation,
					SecurityIdentifier owner,
					SecurityIdentifier group,
					GenericAcl sacl,
					GenericAcl dacl)
		{
			int errorCode;
			int Length;
			byte[] OwnerBinary = null, GroupBinary = null, SaclBinary = null, DaclBinary = null;
			Privilege securityPrivilege = null;

			//
			// Demand unmanaged code permission
			// The integrator layer is free to assert this permission
			// and, in turn, demand another permission of its caller
			//

			new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();

			if (owner != null)
			{
				Length = owner.BinaryLength;
				OwnerBinary = new byte[Length];
				owner.GetBinaryForm(OwnerBinary, 0);
			}

			if (@group != null)
			{
				Length = @group.BinaryLength;
				GroupBinary = new byte[Length];
				@group.GetBinaryForm(GroupBinary, 0);
			}

			if (dacl != null)
			{
				Length = dacl.BinaryLength;
				DaclBinary = new byte[Length];
				dacl.GetBinaryForm(DaclBinary, 0);
			}

			if (sacl != null)
			{
				Length = sacl.BinaryLength;
				SaclBinary = new byte[Length];
				sacl.GetBinaryForm(SaclBinary, 0);
			}

			if ((securityInformation & SecurityInfos.SystemAcl) != 0)
			{
				//
				// Enable security privilege if trying to set a SACL.
				// Note: even setting it by handle needs this privilege enabled!
				//

				securityPrivilege = new Privilege(Privilege.Security);
			}

			// Ensure that the finally block will execute
			RuntimeHelpers.PrepareConstrainedRegions();

			try
			{
				if (securityPrivilege != null)
				{
					try
					{
						securityPrivilege.Enable();
					}
					catch (PrivilegeNotHeldException)
					{
						// we will ignore this exception and press on just in case this is a remote resource
					}
				}

				if (name != null)
				{
					errorCode = (int)NativeMethods.SetSecurityInfoByName(name, (uint)type, (uint)securityInformation, OwnerBinary, GroupBinary, DaclBinary, SaclBinary);
				}
				else if (handle != null)
				{
					if (handle.IsInvalid)
					{
						throw new ArgumentException("Invalid safe handle");
					}
					else
					{
						errorCode = (int)NativeMethods.SetSecurityInfoByHandle(handle, (uint)type, (uint)securityInformation, OwnerBinary, GroupBinary, DaclBinary, SaclBinary);
					}
				}
				else
				{
					// both are null, shouldn't happen
					throw new InvalidProgramException();
				}

				if (errorCode == NativeMethods.ERROR_NOT_ALL_ASSIGNED ||
					errorCode == NativeMethods.ERROR_PRIVILEGE_NOT_HELD)
				{
					throw new PrivilegeNotHeldException(Privilege.Security);
				}
				else if (errorCode == NativeMethods.ERROR_ACCESS_DENIED ||
					errorCode == NativeMethods.ERROR_CANT_OPEN_ANONYMOUS)
				{
					throw new UnauthorizedAccessException();
				}
				else if (errorCode != NativeMethods.ERROR_SUCCESS)
				{
					goto Error;
				}
			}
			catch
			{
				// protection against exception filter-based luring attacks
				if (securityPrivilege != null)
				{
					securityPrivilege.Revert();
				}
				throw;
			}
			finally
			{
				if (securityPrivilege != null)
				{
					securityPrivilege.Revert();
				}
			}

			return 0;

		Error:

			if (errorCode == NativeMethods.ERROR_NOT_ENOUGH_MEMORY)
			{
				throw new OutOfMemoryException();
			}

			return errorCode;
		}
#endif

        public static bool IsPathUnc(string path)
		{
			Uri uri;
			return (!string.IsNullOrEmpty(path) && path.StartsWith(Path.UNCLongPathPrefix, StringComparison.InvariantCultureIgnoreCase)) || (Uri.TryCreate(path, UriKind.Absolute, out uri) && uri.IsUnc);
		}

		public static bool IsPathDots(string path)
		{
			return path == "." || path == "..";
		}
    }
}