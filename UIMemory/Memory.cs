using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;

namespace UIMemory
{
    public class Memory
    {
        private readonly IntPtr UIHandle;      

        public Memory(string UIProcessName)
        {
            string UIName = UIProcessName;
            Process UIMain = Process.GetProcessesByName(UIName)[0];
            UIHandle = WinAPI.OpenProcess((uint)VirtualMemoryProtection.PROCESS_ALL_ACCESS, false, UIMain.Id);
            if (UIHandle == IntPtr.Zero)
                throw new NullReferenceException();
        }

        private byte[] UIReadBytes(IntPtr pOffset, uint pSize)
        {
            try
            {
                uint lpflOldProtect;
                WinAPI.VirtualProtectEx(UIHandle, pOffset, (UIntPtr)pSize, 4U, out lpflOldProtect);
                byte[] lpBuffer = new byte[(int)(IntPtr)pSize];
                WinAPI.ReadProcessMemory(UIHandle, pOffset, lpBuffer, pSize, 0U);
                WinAPI.VirtualProtectEx(UIHandle, pOffset, (UIntPtr)pSize, lpflOldProtect, out lpflOldProtect);
                return lpBuffer;
            }
            catch
            {
                return new byte[1];
            }
        }

        private bool UIWriteBytes(IntPtr pOffset, byte[] pBytes)
        {

            try
            {
                uint lpflOldProtect;
                WinAPI.VirtualProtectEx(UIHandle, pOffset, (UIntPtr)((ulong)pBytes.Length), 4U, out lpflOldProtect);
                bool flag = WinAPI.WriteProcessMemory(UIHandle, pOffset, pBytes, (uint)pBytes.Length, 0U);
                WinAPI.VirtualProtectEx(UIHandle, pOffset, (UIntPtr)((ulong)pBytes.Length), lpflOldProtect, out lpflOldProtect);
                return flag;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Читает из процесса значение по определенному адресу
        /// </summary>
        /// <typeparam name="T">тип значения, которое надо прочитать</typeparam>
        /// <param name="address">адрес для чтения</param>
        /// <returns></returns>
        public unsafe T UIRead<T>(int address)
        {
            var size = MarshalCache<T>.UISize;
            var buffer = UIReadBytes((IntPtr)address, (uint)size);

            fixed (byte* b = buffer)
                return Marshal.PtrToStructure<T>((IntPtr)b);
        }

        /// <summary>
        /// Читает массив данный из определенного адреса
        /// </summary>
        /// <typeparam name="T">тип данных</typeparam>
        /// <param name="address">адрес для чтения</param>
        /// <param name="count">кол-во элементов данных</param>
        /// <returns></returns>
        public T[] Read<T>(int address, int count)
        {
            var size = MarshalCache<T>.UISize;

            var ret = new T[count];
            for (var i = 0; i < count; i++)
                ret[i] = UIRead<T>(address + (i * size));

            return ret;
        }

        /// <summary>
        /// Записывает в память значение по определенному адресу
        /// </summary>
        /// <typeparam name="T">тип значения (необязательно)</typeparam>
        /// <param name="address">адрес для записи</param>
        /// <param name="value">само значение</param>
        public unsafe void UIWrite<T>(int address, T value)
        {
            var size = MarshalCache<T>.UISize;
            var buffer = new byte[size];

            fixed (byte* b = buffer)
                Marshal.StructureToPtr(value, (IntPtr)b, true);

            UIWriteBytes((IntPtr)address, buffer);
        }

        private enum VirtualMemoryProtection : uint
        {
            PAGE_NOACCESS = 1,
            PAGE_READONLY = 2,
            PAGE_READWRITE = 4,
            PAGE_WRITECOPY = 8,
            PAGE_EXECUTE = 16,
            PAGE_EXECUTE_READ = 32,
            PAGE_EXECUTE_READWRITE = 64,
            PAGE_EXECUTE_WRITECOPY = 128,
            PAGE_GUARD = 256,
            PAGE_NOCACHE = 512,
            PROCESS_ALL_ACCESS = 2035711,
        }

        public void UIDispose()
        {
            if (!WinAPI.CloseHandle(UIHandle))
                throw new NullReferenceException();
        }

        static class MarshalCache<T>
        {
            public static readonly int UISize;

            public static readonly Type UIRealType;

            public static TypeCode UITypeCode;


            public static bool TypeRequiresMarshal;

            internal static readonly GetUnsafePtrDelegate GetUnsafePtr;

            static MarshalCache()
            {
                UITypeCode = Type.GetTypeCode(typeof(T));

                if (typeof(T) == typeof(bool))
                {
                    UISize = 1;
                    UIRealType = typeof(T);
                }
                else if (typeof(T).IsEnum)
                {
                    var underlying = typeof(T).GetEnumUnderlyingType();
                    UISize = Marshal.SizeOf(underlying);
                    UIRealType = underlying;
                    UITypeCode = Type.GetTypeCode(underlying);
                }
                else
                {
                    UISize = Marshal.SizeOf(typeof(T));
                    UIRealType = typeof(T);
                }

                TypeRequiresMarshal =
                    UIRealType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(m => m.GetCustomAttributes(typeof(MarshalAsAttribute), true).Any());
                var method = new DynamicMethod(
                    string.Format("GetPinnedPtr<{0}>", typeof(T).FullName.Replace(".", "<>")), typeof(void*),
                    new[] { typeof(T).MakeByRefType() },
                    typeof(MarshalCache<>).Module);
                var generator = method.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Conv_U);
                generator.Emit(OpCodes.Ret);
                GetUnsafePtr = (GetUnsafePtrDelegate)method.CreateDelegate(typeof(GetUnsafePtrDelegate));
            }

            internal unsafe delegate void* GetUnsafePtrDelegate(ref T value);
        }
    }
}
