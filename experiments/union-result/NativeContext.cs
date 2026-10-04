using System.Runtime.InteropServices;

namespace UnionResultLab;
internal static class NativeContext
{
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process,IntPtr job,out bool result);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(IntPtr job,int infoClass,IntPtr buffer,uint length,IntPtr returnedLength);
    [DllImport("advapi32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process,uint desiredAccess,out IntPtr token);
    [DllImport("advapi32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr token,int infoClass,IntPtr buffer,int length,out int required);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    public static object Capture(IntPtr process)
    {
        if(!OperatingSystem.IsWindows()) return new{available=false};
        bool jobQuery=IsProcessInJob(process,IntPtr.Zero,out bool inJob);
        int jobError=jobQuery?0:Marshal.GetLastPInvokeError();
        IntPtr buffer=Marshal.AllocHGlobal(8); int? flags=null,rate=null; int? cpuQueryError=null,integrityRid=null,tokenError=null;
        try
        {
            if(inJob && QueryInformationJobObject(IntPtr.Zero,15,buffer,8,IntPtr.Zero))
            { flags=Marshal.ReadInt32(buffer);rate=Marshal.ReadInt32(buffer,4); }
            else if(inJob) cpuQueryError=Marshal.GetLastPInvokeError();
        }
        finally { Marshal.FreeHGlobal(buffer); }
        if(OpenProcessToken(process,8,out IntPtr token))
        {
            try
            {
                GetTokenInformation(token,25,IntPtr.Zero,0,out int length);
                if(length>0)
                {
                    buffer=Marshal.AllocHGlobal(length);
                    try
                    {
                        if(GetTokenInformation(token,25,buffer,length,out _))
                        {
                            IntPtr sid=Marshal.ReadIntPtr(buffer);
                            byte count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                            if(count>0) integrityRid=Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)(count-1)));
                        }
                        else tokenError=Marshal.GetLastPInvokeError();
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                }
                else tokenError=Marshal.GetLastPInvokeError();
            }
            finally { CloseHandle(token); }
        }
        else tokenError=Marshal.GetLastPInvokeError();
        return new{available=true,jobQuery,inJob,jobError,cpuRateControlFlags=flags,cpuRateValue=rate,cpuQueryError,integrityRid,tokenError,
            limitation="Only current accessible job context; unqueryable or enclosing job limits may remain unknown."};
    }
}
