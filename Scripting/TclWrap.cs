using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace TclWrap {
	public class TclAPI {
		public delegate int TclCommand(IntPtr clientData, IntPtr interp, int argc, IntPtr argsPtr);
    	
		[DllImport("tcl84.dll")] public static extern IntPtr Tcl_CreateInterp();
		[DllImport("tcl84.dll")] public static extern int Tcl_Eval(IntPtr interp, string script);
		[DllImport("tcl84.dll")] public static extern void Tcl_SetResult(IntPtr interp, string result, IntPtr method);
		[DllImport("tcl84.dll")] public static extern IntPtr Tcl_GetObjResult(IntPtr interp);
		[DllImport("tcl84.dll")] public static extern string Tcl_GetStringFromObj(IntPtr tclObj, IntPtr length);
		[DllImport("tcl84.dll")] public static extern IntPtr Tcl_CreateCommand(IntPtr interp, string name, IntPtr cmdProc, IntPtr clientData, IntPtr cmdDeleteProc);
		[DllImport("tcl84.dll")] public static extern void Tcl_DeleteInterp(IntPtr interp);
		
		public const int TCL_OK = 0;
		public const int TCL_ERROR = 1;
		public const int TCL_RETURN = 2;
		public const int TCL_BREAK = 3;
		public const int TCL_CONTINUE = 4;
		
        // Here be dragons. If anything breaks, it's SM's fault.
		unsafe public static string[] GetArgumentArray(int argc, IntPtr argv) {
			char ** argPtr = (char **) argv.ToPointer();
			List<string> result = new List<string>();
			for(int i = 0; i < argc; ++i) {
				result.Add(Marshal.PtrToStringAnsi((IntPtr)(argPtr[i])));
			}
			return result.ToArray();
		}
		
		public static void SetResult(IntPtr interp, string result) {
			// (IntPtr) 1 is TCL_VOLATILE, meaning 'result' might not hang around after the call is complete,
			// which it probably won't, given .NET and all that
			TclAPI.Tcl_SetResult(interp, result, (IntPtr) 1);
		}
	}

	public class TclInterpreter {
		private IntPtr interp;

		public TclInterpreter() {
			interp = TclAPI.Tcl_CreateInterp();
			if (interp == IntPtr.Zero) {
				throw new SystemException("Unable to initalize Tcl interpreter");
			}
		}
		
		~TclInterpreter() {
			if(interp != IntPtr.Zero) {
				Close();
			}
		}
		
		public void Close() {
			TclAPI.Tcl_DeleteInterp(interp);
			interp = IntPtr.Zero;
		}

		public int EvalScript(string script) {
			if (interp == IntPtr.Zero) {
				throw new SystemException("Attempted to call a closed Tcl interpeter!");
			}
			return TclAPI.Tcl_Eval(interp, script);
		}
		
		public int SourceFile(string filename) {
			return EvalScript(File.ReadAllText(filename));
		}

		public void CreateCommand(string commandName, TclAPI.TclCommand cmd) {
			if (interp == IntPtr.Zero) {
				throw new SystemException("Attempted to call a closed Tcl interpeter!");
			}
			TclAPI.Tcl_CreateCommand(interp, commandName, Marshal.GetFunctionPointerForDelegate(cmd), IntPtr.Zero, IntPtr.Zero);
		}

		public string Result {
			get {
				if (interp == IntPtr.Zero) {
					throw new SystemException("Attempted to call a closed Tcl interpeter!");
				}
				IntPtr obj = TclAPI.Tcl_GetObjResult(interp);
				if (obj == IntPtr.Zero) {
					return "";
				} else {
					return TclAPI.Tcl_GetStringFromObj(obj,IntPtr.Zero);
				}
			}
		}
	}
}