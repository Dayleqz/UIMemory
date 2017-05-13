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

        private byte[] UIReadBytes(IntPtr UIOffset, uint UISize)
        {
            try
            {
                uint UIOldProtect;
                WinAPI.VirtualProtectEx(UIHandle, UIOffset, (UIntPtr)UISize, 4U, out UIOldProtect);
                byte[] UIBuffer = new byte[(int)(IntPtr)UISize];
                WinAPI.ReadProcessMemory(UIHandle, UIOffset, UIBuffer, UISize, 0U);
                WinAPI.VirtualProtectEx(UIHandle, UIOffset, (UIntPtr)UISize, UIOldProtect, out UIOldProtect);
                return UIBuffer;
            }
            catch
            {
                return new byte[1];
            }
        }

        private bool UIWriteBytes(IntPtr UIOffset, byte[] UIBytes)
        {

            try
            {
                uint UIOldProtect;
                WinAPI.VirtualProtectEx(UIHandle, UIOffset, (UIntPtr)((ulong)UIBytes.Length), 4U, out UIOldProtect);
                bool UIFlag = WinAPI.WriteProcessMemory(UIHandle, UIOffset, UIBytes, (uint)UIBytes.Length, 0U);
                WinAPI.VirtualProtectEx(UIHandle, UIOffset, (UIntPtr)((ulong)UIBytes.Length), UIOldProtect, out UIOldProtect);
                return UIFlag;
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
        /// <param name="UIAdress">адрес для чтения</param>
        /// <returns></returns>
        public unsafe T UIRead<T>(int UIAdress)
        {
            var UISize = UIMarshalCache<T>.UISize;
            var UIBuffer = UIReadBytes((IntPtr)UIAdress, (uint)UISize);

            fixed (byte* b = UIBuffer)
                return Marshal.PtrToStructure<T>((IntPtr)b);
        }

        /// <summary>
        /// Читает массив данный из определенного адреса
        /// </summary>
        /// <typeparam name="T">тип данных</typeparam>
        /// <param name="UIAdress">адрес для чтения</param>
        /// <param name="UICount">кол-во элементов данных</param>
        /// <returns></returns>
        public T[] Read<T>(int UIAdress, int UICount)
        {
            var UISize = UIMarshalCache<T>.UISize;

            var UIRet = new T[UICount];
            for (var UINT = 0; UINT < UICount; UINT++)
                UIRet[UINT] = UIRead<T>(UIAdress + (UINT * UISize));

            return UIRet;
        }

        /// <summary>
        /// Записывает в память значение по определенному адресу
        /// </summary>
        /// <typeparam name="T">тип значения (необязательно)</typeparam>
        /// <param name="UIAdress">адрес для записи</param>
        /// <param name="UIValue">само значение</param>
        public unsafe void UIWrite<T>(int UIAdress, T UIValue)
        {
            var UISize = UIMarshalCache<T>.UISize;
            var UIBuffer = new byte[UISize];

            fixed (byte* UIByte = UIBuffer)
                Marshal.StructureToPtr(UIValue, (IntPtr)UIByte, true);

            UIWriteBytes((IntPtr)UIAdress, UIBuffer);
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

        static class UIMarshalCache<T>
        {
            public static readonly int UISize;

            public static readonly Type UIRealType;

            public static TypeCode UITypeCode;

            public static bool UITypeRequiresMarshal;

            internal static readonly GetUnsafePtrDelegate UIGetUnsafePtr;

            static UIMarshalCache()
            {
                UITypeCode = Type.GetTypeCode(typeof(T));

                if (typeof(T) == typeof(bool))
                {
                    UISize = 1;
                    UIRealType = typeof(T);
                }
                else if (typeof(T).IsEnum)
                {
                    var UIUnderLiyng = typeof(T).GetEnumUnderlyingType();
                    UISize = Marshal.SizeOf(UIUnderLiyng);
                    UIRealType = UIUnderLiyng;
                    UITypeCode = Type.GetTypeCode(UIUnderLiyng);
                }
                else
                {
                    UISize = Marshal.SizeOf(typeof(T));
                    UIRealType = typeof(T);
                }

                UITypeRequiresMarshal = UIRealType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(m => m.GetCustomAttributes(typeof(MarshalAsAttribute), true).Any());
                var method = new DynamicMethod(string.Format("GetPinnedPtr<{0}>", typeof(T).FullName.Replace(".", "<>")), typeof(void*), new[]
                {
                    typeof(T).MakeByRefType()
                },
                    typeof(UIMarshalCache<>).Module);
                var UIGenerator = method.GetILGenerator();
                UIGenerator.Emit(OpCodes.Ldarg_0);
                UIGenerator.Emit(OpCodes.Conv_U);
                UIGenerator.Emit(OpCodes.Ret);
                UIGetUnsafePtr = (GetUnsafePtrDelegate)method.CreateDelegate(typeof(GetUnsafePtrDelegate));
            }
            internal unsafe delegate void* GetUnsafePtrDelegate(ref T UIValue);
        }
    }
}
