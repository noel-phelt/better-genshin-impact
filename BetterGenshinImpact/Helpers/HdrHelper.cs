using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.Helpers;

public static class HdrHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    public enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
    {
        DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9,
        DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // bit 0 = advanced color supported, bit 1 = advanced color enabled
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint enableAdvancedColor; // 1 to enable, 0 to disable
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRate_Numerator;
        public uint refreshRate_Denominator;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        public uint union_1;
        public uint union_2;
        public uint union_3;
        public uint union_4;
        public uint union_5;
        public uint union_6;
        public uint union_7;
        public uint union_8;
        public uint union_9;
        public uint union_10;
        public uint union_11;
        public uint union_12;
        public uint union_13;
        public uint union_14;
        public uint union_15;
        public uint union_16;
    }

    [DllImport("User32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("User32.dll")]
    public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    [DllImport("User32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("User32.dll")]
    public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE requestPacket);

    public const uint QDC_ONLY_ACTIVE_PATHS = 2;

    /// <summary>
    /// Gets the global HDR status. Returns true if at least one active display has HDR enabled.
    /// </summary>
    public static bool IsHdrEnabled()
    {
        uint numPathArrayElements = 0;
        uint numModeInfoArrayElements = 0;

        int res = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPathArrayElements, out numModeInfoArrayElements);
        if (res != 0) return false;

        var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        res = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathArray, ref numModeInfoArrayElements, modeArray, IntPtr.Zero);
        if (res != 0) return false;

        for (int i = 0; i < numPathArrayElements; i++)
        {
            var path = pathArray[i];
            var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
            colorInfo.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
            colorInfo.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
            colorInfo.header.adapterId = path.targetInfo.adapterId;
            colorInfo.header.id = path.targetInfo.id;

            if (DisplayConfigGetDeviceInfo(ref colorInfo) == 0)
            {
                bool enabled = (colorInfo.value & 2) == 2;
                if (enabled) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Turns HDR on or off for all supported active displays.
    /// </summary>
    public static bool SetHdrState(bool enable, ILogger? logger = null)
    {
        uint numPathArrayElements = 0;
        uint numModeInfoArrayElements = 0;

        int res = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out numPathArrayElements, out numModeInfoArrayElements);
        if (res != 0)
        {
            logger?.LogError($"GetDisplayConfigBufferSizes failed with code {res}");
            return false;
        }

        var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        res = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathArray, ref numModeInfoArrayElements, modeArray, IntPtr.Zero);
        if (res != 0)
        {
            logger?.LogError($"QueryDisplayConfig failed with code {res}");
            return false;
        }

        bool anySuccess = false;

        for (int i = 0; i < numPathArrayElements; i++)
        {
            var path = pathArray[i];
            var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
            colorInfo.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
            colorInfo.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
            colorInfo.header.adapterId = path.targetInfo.adapterId;
            colorInfo.header.id = path.targetInfo.id;

            if (DisplayConfigGetDeviceInfo(ref colorInfo) == 0)
            {
                bool supported = (colorInfo.value & 1) == 1;
                bool currentlyEnabled = (colorInfo.value & 2) == 2;

                if (supported && currentlyEnabled != enable)
                {
                    var setColor = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
                    setColor.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
                    setColor.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE));
                    setColor.header.adapterId = path.targetInfo.adapterId;
                    setColor.header.id = path.targetInfo.id;
                    setColor.enableAdvancedColor = enable ? 1u : 0u;

                    int setRes = DisplayConfigSetDeviceInfo(ref setColor);
                    if (setRes == 0)
                    {
                        anySuccess = true;
                        logger?.LogInformation($"Successfully set HDR to {enable} for display {i}");
                    }
                    else
                    {
                        logger?.LogError($"Failed to set HDR to {enable} for display {i}, code {setRes}");
                    }
                }
            }
        }

        return anySuccess;
    }
}
