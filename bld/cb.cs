using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public static class cb
{
    enum VCVersion
    {
        v110,
        v120,
        v140,
        v141,
        v142,
    }

    enum Machine
    {
        x86,
        x64,
        arm,
        arm64,
        mips64,
    }

    enum Flavor
    {
        plain,
        appcontainer,
        xp,
        wp80,
        wp81,
    }

    static string get_crt_option(VCVersion v, Flavor f)
    {
        switch (f)
        {
            case Flavor.wp80:
            case Flavor.wp81:
                return "/MD";
            case Flavor.appcontainer:
                return "/MD";
            default:
                return "/MT";
        }
    }

    static string get_vcvarsbat(VCVersion v, Flavor f)
    {
        if (f == Flavor.wp80)
        {
            if (v == VCVersion.v110)
            {
                return "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\WPSDK\\WP80\\vcvarsphoneall.bat";
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        switch (v)
        {
            case VCVersion.v110:
                return "C:\\Program Files (x86)\\Microsoft Visual Studio 11.0\\VC\\vcvarsall.bat";
            case VCVersion.v120:
                return "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\VC\\vcvarsall.bat";
            case VCVersion.v140:
                return "C:\\Program Files (x86)\\Microsoft Visual Studio 14.0\\VC\\vcvarsall.bat";
            case VCVersion.v141:
                return "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community\\VC\\Auxiliary\\Build\\vcvarsall.bat";
            case VCVersion.v142:
                return "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\VC\\Auxiliary\\Build\\vcvarsall.bat";
            default:
                throw new NotImplementedException();
        }
    }

    static string get_toolchain(VCVersion v, Machine m)
    {
        switch (v)
        {
            case VCVersion.v110:
            case VCVersion.v120:
            case VCVersion.v140:
                switch (m)
                {
                    case Machine.x86:
                        return "x86";
                    case Machine.x64:
                        return "x86_amd64";
                    case Machine.arm:
                        return "x86_arm";
                    default:
                        throw new NotImplementedException();
                }
            case VCVersion.v141:
            case VCVersion.v142:
                switch (m)
                {
                    case Machine.x86:
                        return "x86";
                    case Machine.x64:
                        return "x64";
                    case Machine.arm:
                        return "x64_arm";
                    case Machine.arm64:
                        return "x64_arm64";
                    case Machine.mips64:
                        return "x64_mips64";
                    default:
                        throw new NotImplementedException();
                }
            default:
                throw new NotImplementedException();
        }
    }

// sudo apt-get install gcc-arm-linux-gnueabihf
// sudo apt-get install musl-dev musl-tools
// sudo apt-get install gcc-aarch64-linux-gnu

    static void write_linux(
        string libname,
        linux_target t,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var subdir = t.subdir(libname);
        var dest_sh = t.sh(libname);
        var dest_gccargs = t.gccargs(libname);
        string compiler;
        using (TextWriter tw = new StreamWriter(dest_gccargs))
        {
            switch (t.target)
            {
                case "x64":
                    compiler = "gcc";
                    tw.Write(" -m64\n");
                    tw.Write(" -msse4.2\n");
                    tw.Write(" -maes\n");
                    break;

                case "x86":
                    compiler = "gcc";
                    tw.Write(" -m32\n");
                    tw.Write(" -msse4.2\n");
                    tw.Write(" -maes\n");
                    break;

                case "arm64":
                    compiler = "aarch64-linux-gnu-gcc";
                    break;

                case "mips64":
                    compiler = "mips64el-linux-gnuabi64-gcc";
                    break;

                case "s390x":
                    compiler = "s390x-linux-gnu-gcc";
                    break;

                case "ppc64le":
                    compiler = "powerpc64le-linux-gnu-gcc";
                    break;

                case "armhf":
                    compiler = "arm-linux-gnueabihf-gcc";
                    break;

                case "armsf":
                    compiler = "arm-linux-gnueabi-gcc";
                    break;

                case "musl-x64":
                    compiler = "musl-gcc";
                    tw.Write(" -m64\n");
                    tw.Write(" -msse4.2\n");
                    tw.Write(" -maes\n");
                    break;

                case "musl-armhf":
                    compiler = "arm-linux-musleabihf-cc";
                    break;

                case "musl-arm64":
                    compiler = "aarch64-linux-musl-cc";
                    break;

                default:
                    throw new NotImplementedException();
            }
            tw.Write(" -shared\n");
            tw.Write(" -fPIC\n");
            tw.Write(" -O\n");
            foreach (var d in defines.Keys.OrderBy(q => q))
            {
                var v = defines[d];
                tw.Write(" -D{0}", d);
                if (v != null)
                {
                    tw.Write("={0}", v);
                }
                tw.Write("\n");
            }
            tw.Write(" -DNDEBUG\n");
            foreach (var p in includes.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" -I{0}\n", p);
            }
            tw.Write(" -o \"bin/{1}/lib{0}.so\"\n", libname, subdir);
            foreach (var s in cfiles.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" {0}\n", s);
            }
            foreach (var s in libs.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" {0}\n", s);
            }
        }
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
            tw.Write("mkdir -p \"./bin/{0}\"\n", subdir);
            tw.Write("{0} @{1}\n", compiler, dest_gccargs);
        }
    }

    static void write_android_ndk_build(
        string libname,
        IList<android_target> targets,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_dir = string.Format("android_{0}", libname);
        Directory.CreateDirectory(dest_dir);
        var dest_dir_jni = Path.Combine(dest_dir, "jni");
        Directory.CreateDirectory(dest_dir_jni);
        var dest_android_mk = Path.Combine(dest_dir_jni, "Android.mk");
        using (TextWriter tw = new StreamWriter(dest_android_mk))
        {
            var defs = defines
                .Select(p => (p.Value == null) ? string.Format("-D{0}", p.Key) : string.Format("-D{0}={1}", p.Key, p.Value))
                .ToArray();
            var src = cfiles
                .Select(x => Path.Combine("..", "..", x))
                .Select(x => x.Replace("\\", "/"))
                .ToArray();
            var inc = includes
                .Select(x => Path.Combine("..", "..", x))
                .Select(x => x.Replace("\\", "/"))
                .Select(x => string.Format("$(LOCAL_PATH)/{0}", x))
                .ToArray();
            tw.Write("LOCAL_PATH := $(call my-dir)\n");
            tw.Write("include $(CLEAR_VARS)\n");
            tw.Write("LOCAL_MODULE := lib{0}\n", libname);
            tw.Write("LOCAL_MODULE_FILENAME := lib{0}\n", libname);
            tw.Write("LOCAL_CFLAGS := -O {0}\n", string.Join(" ", defs));
            tw.Write("ifeq ($(TARGET_ARCH_ABI),x86)\nLOCAL_CFLAGS += -maes -msse4.2\nendif\n");
            tw.Write("ifeq ($(TARGET_ARCH_ABI),x86_64)\nLOCAL_CFLAGS += -maes -msse4.2\nendif\n");
            tw.Write("LOCAL_LDLIBS := -llog\n");
            if (includes.Count > 0)
            {
                tw.Write("LOCAL_C_INCLUDES := {0}\n", string.Join(" ", inc));
            }
            tw.Write("LOCAL_SRC_FILES = {0}\n", string.Join(" ", src));
            tw.Write("include $(BUILD_SHARED_LIBRARY)\n");
        }
        var dest_application_mk = Path.Combine(dest_dir_jni, "Application.mk");
        using (TextWriter tw = new StreamWriter(dest_application_mk))
        {
            var targs = targets
                .Select(x => x.target)
                .ToArray();
            tw.Write("APP_ABI := {0}\n", string.Join(" ", targs));
        }
        var dest_project_properties = Path.Combine(dest_dir, "project.properties");
        using (TextWriter tw = new StreamWriter(dest_project_properties))
        {
            tw.Write("target=android-21\n");
        }
    }

    static void write_android(
        string libname,
        android_target t,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var subdir = t.subdir(libname);
        var dest_sh = t.sh(libname);
        var dest_gccargs = t.gccargs(libname);
        string compiler;
        using (TextWriter tw = new StreamWriter(dest_gccargs))
        {
            switch (t.target)
            {
                case "arm64-v8a":
                    compiler = "/Users/eric/android_toolchains/arm64/bin/aarch64-linux-android-gcc";
                    break;

                case "armeabi":
                    compiler = "/Users/eric/android_toolchains/arm/bin/arm-linux-androideabi-gcc";
                    tw.Write(" -march=armv5te\n");
                    tw.Write(" -mthumb\n");
                    tw.Write(" -msoft-float\n");
                    break;

                case "armeabi-v7a":
                    compiler = "/Users/eric/android_toolchains/arm/bin/arm-linux-androideabi-gcc";
                    tw.Write(" -march=armv7-a\n");
                    tw.Write(" -mthumb\n");
                    break;

                case "x86":
                    compiler = "/Users/eric/android_toolchains/x86/bin/i686-linux-android-gcc";
                    break;

                case "x86_64":
                    compiler = "/Users/eric/android_toolchains/x86_64/bin/x86_64-linux-android-gcc";
                    break;

                default:
                    throw new NotImplementedException();
            }
            tw.Write(" -shared\n");
            tw.Write(" -fPIC\n");
            tw.Write(" -O\n");
            foreach (var d in defines.Keys.OrderBy(q => q))
            {
                var v = defines[d];
                tw.Write(" -D{0}", d);
                if (v != null)
                {
                    tw.Write("={0}", v);
                }
                tw.Write("\n");
            }
            tw.Write(" -DNDEBUG\n");
            foreach (var p in includes.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" -I{0}\n", p);
            }
            tw.Write(" -o \"bin/{1}/lib{0}.so\"\n", libname, subdir);
            foreach (var s in cfiles.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" {0}\n", s);
            }
            foreach (var s in libs.Select(x => x.Replace("\\", "/")))
            {
                tw.Write(" {0}\n", s);
            }
        }
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
            tw.Write("mkdir -p \"./bin/{0}\"\n", subdir);
            tw.Write("{0} @{1}\n", compiler, dest_gccargs);
        }
    }

    static void write_wasm(
        string libname,
        IList<string> cfiles,
        Dictionary<string, string> defines,
        IList<string> includes,
        IList<string> libs)
    {
        var dest_filelist = string.Format("wasm_{0}.libtoolfiles", libname);
        using (TextWriter tw = new StreamWriter(dest_filelist))
        {
            var subdir = string.Format("{0}/wasm", libname);
            foreach (var s in cfiles)
            {
                var b = Path.GetFileNameWithoutExtension(s);
                var o = string.Format("./obj/{0}/{1}.o", subdir, b);
                tw.Write("{0}\n", o);
            }
        }
        using (TextWriter tw = new StreamWriter(string.Format("wasm_{0}.sh", libname)))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            var subdir = string.Format("{0}/wasm", libname);
            tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
            foreach (var s in cfiles)
            {
                tw.Write("emcc");
                tw.Write(" -Oz");
                foreach (var d in defines.Keys.OrderBy(q => q))
                {
                    var v = defines[d];
                    tw.Write(" -D{0}", d);
                    if (v != null)
                    {
                        tw.Write("={0}", v);
                    }
                }
                foreach (var p in includes)
                {
                    tw.Write(" -I{0}", p);
                }
                tw.Write(" -c");
                var b = Path.GetFileNameWithoutExtension(s);
                tw.Write(" -o ./obj/{0}/{1}.o", subdir, b);
                tw.Write(" {0}\n", s);
            }
            tw.Write("mkdir -p \"./bin/{0}/$1\"\n", subdir);
            tw.Write("emar rcs ./bin/{0}/$1/{1}.a @wasm_{1}.libtoolfiles\n", subdir, libname);
        }
    }

    static void write_maccatalyst_dynamic(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_sh = string.Format("maccatalyst_dynamic_{0}.sh", libname);
        var arches = new string[] {
            "x86_64",
            "arm64"
        };
        StringBuilder archLibraries = new StringBuilder();
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            foreach (string arch in arches)
            {
                tw.Write("mkdir -p \"./bin/{0}/maccatalyst/{1}\"\n", libname, arch);
                tw.Write("xcrun");
                tw.Write(" --sdk macosx");
                tw.Write(" clang");
                tw.Write(" -dynamiclib");
                tw.Write(" -O");
                tw.Write(" -target x86_64-apple-ios-macabi");
                tw.Write(" -mmacosx-version-min=10.14");
                tw.Write(" -arch {0}", arch);
                if (arch == "x86_64" )
                {
                    tw.Write(" -msse4.2");
                    tw.Write(" -maes");
                }
                tw.Write(" -framework Security");
                foreach (var d in defines.Keys.OrderBy(q => q))
                {
                    var v = defines[d];
                    tw.Write(" -D{0}", d);
                    if (v != null)
                    {
                        tw.Write("={0}", v);
                    }
                }
                foreach (var p in includes)
                {
                    tw.Write(" -I{0}", p);
                }
                tw.Write(" -o ./bin/{0}/maccatalyst/{1}/lib{0}.dylib", libname, arch);
                foreach (var s in cfiles)
                {
                    tw.Write(" {0}", s);
                }
                tw.Write(" -lc");
                tw.Write(" \n");
                archLibraries.AppendFormat("./bin/{0}/maccatalyst/{1}/lib{0}.dylib ", libname, arch);
            }
            // Create a universal binary from each of the architectures
            tw.Write("lipo {1} -create -output ./bin/{0}/maccatalyst/lib{0}.dylib\n", libname, archLibraries);
        }
    }

    static void write_mac_dynamic(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_sh = string.Format("mac_dynamic_{0}.sh", libname);
        var arches = new string[] {
            "x86_64",
            "arm64"
        };
        StringBuilder archLibraries = new StringBuilder();
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            foreach (string arch in arches)
            {
                tw.Write("mkdir -p \"./bin/{0}/mac/{1}\"\n", libname, arch);
                tw.Write("xcrun");
                tw.Write(" --sdk macosx");
                tw.Write(" clang");
                tw.Write(" -dynamiclib");
                tw.Write(" -O");
                tw.Write(" -mmacosx-version-min=10.14");
                tw.Write(" -arch {0}", arch);
                if (arch == "x86_64" )
                {
                    tw.Write(" -msse4.2");
                    tw.Write(" -maes");
                }
                tw.Write(" -framework Security");
                foreach (var d in defines.Keys.OrderBy(q => q))
                {
                    var v = defines[d];
                    tw.Write(" -D{0}", d);
                    if (v != null)
                    {
                        tw.Write("={0}", v);
                    }
                }
                foreach (var p in includes)
                {
                    tw.Write(" -I{0}", p);
                }
                tw.Write(" -o ./bin/{0}/mac/{1}/lib{0}.dylib", libname, arch);
                foreach (var s in cfiles)
                {
                    tw.Write(" {0}", s);
                }
                tw.Write(" -lc");
                tw.Write(" \n");
                archLibraries.AppendFormat("./bin/{0}/mac/{1}/lib{0}.dylib ", libname, arch);
            }
            // Create a universal binary from each of the architectures
            tw.Write("lipo {1} -create -output ./bin/{0}/mac/lib{0}.dylib\n", libname, archLibraries);
        }
    }

    static void write_mac_static(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_sh = string.Format("mac_static_{0}.sh", libname);
        var arches = new string[] {
            "x86_64",
            "arm64"
        };
        StringBuilder archLibraries = new StringBuilder();
        foreach (var arch in arches)
        {
            var dest_filelist = string.Format("mac_{0}_{1}.libtoolfiles", libname, arch);
            using (TextWriter tw = new StreamWriter(dest_filelist))
            {
                var subdir = string.Format("{0}/mac/{1}", libname, arch);
                foreach (var s in cfiles)
                {
                var b = Path.GetFileNameWithoutExtension(s);
                    var o = string.Format("./obj/{0}/{1}.o", subdir, b);
                    tw.Write("{0}\n", o);
                }
            }
        }
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            foreach (var arch in arches)
            {
                var subdir = string.Format("{0}/mac/{1}", libname, arch);
                tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
                    foreach (var s in cfiles)
                {
                    tw.Write("xcrun");
                    tw.Write(" --sdk macosx");
                    tw.Write(" clang");
                    tw.Write(" -O");
                    tw.Write(" -mmacosx-version-min=10.14");
                    tw.Write(" -arch {0}", arch);
                    if (arch == "x86_64" )
                    {
                        tw.Write(" -msse4.2");
                        tw.Write(" -maes");
                    }
// TODO: -framework Security required?
//                    tw.Write(" -framework Security");
                    foreach (var d in defines.Keys.OrderBy(q => q))
                    {
                        var v = defines[d];
                        tw.Write(" -D{0}", d);
                        if (v != null)
                        {
                            tw.Write("={0}", v);
                        }
                    }
                    foreach (var p in includes)
                    {
                        tw.Write(" -I{0}", p);
                    }
                    tw.Write(" -c");
                var b = Path.GetFileNameWithoutExtension(s);
                    tw.Write(" -o ./obj/{0}/{1}.o", subdir, b);
                    tw.Write(" {0}\n", s);
                }
                tw.Write("libtool -static -o ./bin/{0}/mac/{1}/{0}.a -filelist mac_{0}_{1}.libtoolfiles\n", libname, arch);
                archLibraries.AppendFormat("./bin/{0}/mac/{1}/{0}.a ", libname, arch);
            }
            // Create a universal binary from each of the architectures
            tw.Write("lipo {1} -create -output ./bin/{0}/mac/{0}.a\n", libname, archLibraries);
        }
    }

    static void write_tvos(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_sh = string.Format("tvos_{0}.sh", libname);
        var arches_simulator = new string[] {
            //"i386",
            "x86_64",
        };
        var arches_device = new string[] {
            "arm64",
            //"armv7",
            //"armv7s",
        };
        var arches = arches_simulator.Concat(arches_device).ToArray();
        var dest_filelist = string.Format("tvos_{0}.libtoolfiles", libname);
        using (TextWriter tw = new StreamWriter(dest_filelist))
        {
            foreach (var arch in arches)
            {
                var subdir = string.Format("{0}/tvos/{1}", libname, arch);
                foreach (var s in cfiles)
                {
                var b = Path.GetFileNameWithoutExtension(s);
                    var o = string.Format("./obj/{0}/{1}.o", subdir, b);
                    tw.Write("{0}\n", o);
                }
            }
        }
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            tw.Write("mkdir -p \"./bin/{0}/tvos\"\n", libname);
            foreach (var arch in arches)
            {
                var subdir = string.Format("{0}/tvos/{1}", libname, arch);
                tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
                    foreach (var s in cfiles)
                {
                    tw.Write("xcrun");
                    switch (arch)
                    {
                        case "i386":
                        case "x86_64":
                            tw.Write(" --sdk appletvsimulator");
                            break;
                        case "arm64":
                        case "armv7":
                        case "armv7s":
                            tw.Write(" --sdk appletvos");
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    tw.Write(" clang");
                    tw.Write(" -O");
                    tw.Write(" -arch {0}", arch);
                    if (arch == "i386" || arch == "x86_64" )
                    {
                        tw.Write(" -msse4.2");
                        tw.Write(" -maes");
                    }
// TODO: -framework Security required?
//                    tw.Write(" -framework Security");
                    tw.Write(" -fembed-bitcode");
                    foreach (var d in defines.Keys.OrderBy(q => q))
                    {
                        var v = defines[d];
                        tw.Write(" -D{0}", d);
                        if (v != null)
                        {
                            tw.Write("={0}", v);
                        }
                    }
                    foreach (var p in includes)
                    {
                        tw.Write(" -I{0}", p);
                    }
                    tw.Write(" -c");
                var b = Path.GetFileNameWithoutExtension(s);
                    tw.Write(" -o ./obj/{0}/{1}.o", subdir, b);
                    tw.Write(" {0}\n", s);
                }
            }
            var path_static = $"./bin/{libname}/tvos/{libname}.a";
            tw.Write($"libtool -static -o {path_static} -filelist {dest_filelist}\n");

            tw.Write("mkdir -p \"./bin/{0}/tvos/device\"\n", libname);
            tw.Write($"xcrun --sdk appletvos clang {string.Join(" ", arches_device.Select(s => $"-arch {s}"))} -framework Security -shared -all_load -o ./bin/{libname}/tvos/device/lib{libname}.dylib {path_static}\n");

            tw.Write("mkdir -p \"./bin/{0}/tvos/simulator\"\n", libname);
            tw.Write($"xcrun --sdk appletvsimulator clang {string.Join(" ", arches_simulator.Select(s => $"-arch {s}"))} -framework Security -shared -all_load -o ./bin/{libname}/tvos/simulator/lib{libname}.dylib {path_static}\n");
        }
    }

    static void write_ios_arches(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs,
        TextWriter tw,
        bool simulator,
        string[] arches
        )
    {
        var subfolder_name = simulator ? "simulator" : "device";
        var dest_filelist = $"ios_{subfolder_name}_{libname}.libtoolfiles";
        using (TextWriter tw_filelist = new StreamWriter(dest_filelist))
        {
            foreach (var arch in arches)
            {
                var subdir = $"{libname}/ios/{subfolder_name}/{arch}";
                foreach (var s in cfiles)
                {
                    var b = Path.GetFileNameWithoutExtension(s);
                    var o = $"./obj/{subdir}/{b}.o";
                    tw_filelist.Write($"{o}\n");
                }
            }
        }

        tw.Write($"mkdir -p \"./bin/{libname}/ios/{subfolder_name}\"\n");
        foreach (var arch in arches)
        {
            var subdir = $"{libname}/ios/{subfolder_name}/{arch}";
            tw.Write("mkdir -p \"./obj/{0}\"\n", subdir);
            foreach (var s in cfiles)
            {
                tw.Write("xcrun");
                tw.Write(simulator ? " --sdk iphonesimulator" : " --sdk iphoneos");
                tw.Write(" clang");
                tw.Write(" -O");
                if (simulator)
                    tw.Write(" -mios-simulator-version-min=6.0");
                else
                    tw.Write(" -miphoneos-version-min=6.0");
                tw.Write(" -arch {0}", arch);
                if (arch == "i386" || arch == "x86_64" )
                {
                    tw.Write(" -msse4.2");
                    tw.Write(" -maes");
                }
                tw.Write(" -framework Security");
                foreach (var d in defines.Keys.OrderBy(q => q))
                {
                    var v = defines[d];
                    tw.Write(" -D{0}", d);
                    if (v != null)
                    {
                        tw.Write("={0}", v);
                    }
                }
                foreach (var p in includes)
                {
                    tw.Write(" -I{0}", p);
                }
                tw.Write(" -c");
                var b = Path.GetFileNameWithoutExtension(s);
                tw.Write(" -o ./obj/{0}/{1}.o", subdir, b);
                tw.Write(" {0}\n", s);
            }
        }
        var path_static = $"./bin/{libname}/ios/{subfolder_name}/{libname}.a";

        tw.Write($"mkdir -p \"./bin/{libname}/ios/{subfolder_name}\"\n");
        tw.Write($"libtool -static -o {path_static} -filelist {dest_filelist}\n");
        tw.Write($"xcrun --sdk {(simulator ? "iphonesimulator" : "iphoneos")} clang {string.Join(" ", arches.Select(s => $"-arch {s}"))} -framework Security -shared -all_load -o ./bin/{libname}/ios/{subfolder_name}/lib{libname}.dylib {path_static}\n");
    }

    static void write_ios(
        string libname,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var dest_sh = string.Format("ios_{0}.sh", libname);
        var arches_simulator = new string[] {
            "i386",
            "x86_64",
            "arm64",
        };
        var arches_device = new string[] {
            "arm64",
            "armv7",
            "armv7s",
        };
        using (TextWriter tw = new StreamWriter(dest_sh))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            write_ios_arches(libname, cfiles, defines, includes, libs, tw, true, arches_simulator);
            write_ios_arches(libname, cfiles, defines, includes, libs, tw, false, arches_device);
        }
    }

    static void write_win(
        string libname,
        win_target t,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        var vcversion = t.v;
        var flavor = t.f;
        var machine = t.m;

        var vcvarsbat = get_vcvarsbat(vcversion, flavor);
        var toolchain = get_toolchain(vcversion, machine);
        var crt_option = get_crt_option(vcversion, flavor);
        var subdir = t.subdir(libname);
        var dest_bat = t.bat(libname);
        var dest_linkargs = t.linkargs(libname);
        using (TextWriter tw = new StreamWriter(dest_linkargs))
        {
            tw.Write(" /nologo");
            tw.Write(" /OUT:\"bin\\{1}\\{0}.dll\"", libname, subdir);
            if (flavor == Flavor.xp)
            {
                switch (machine)
                {
                    case Machine.x86:
                        tw.Write(" /SUBSYSTEM:CONSOLE,\"5.01\"");
                        break;
                    case Machine.x64:
                        tw.Write(" /SUBSYSTEM:CONSOLE,\"5.02\"");
                        break;
                    case Machine.arm:
                        tw.Write(" /SUBSYSTEM:CONSOLE,\"6.02\"");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                tw.Write(" /SUBSYSTEM:CONSOLE");
            }
            tw.Write(" /DEBUG:FULL");
            tw.Write(" /DEBUGTYPE:CV,FIXUP");
            tw.Write(" /INCREMENTAL:NO");
            tw.Write(" /OPT:REF");
            tw.Write(" /OPT:ICF");
            tw.Write(" /TLBID:1");
            tw.Write(" /guard:cf");
            tw.Write(" /WINMD:NO");
            tw.Write(" /DYNAMICBASE");
            tw.Write(" /NXCOMPAT");
            tw.Write(" /MACHINE:{0}", machine.ToString().ToUpper());
            tw.Write(" /DLL");
            switch (flavor)
            {
                case Flavor.appcontainer:
                case Flavor.wp81:
                    tw.Write(" /APPCONTAINER");
                    break;
                default:
                    break;
            }
            if (flavor == Flavor.wp80)
            {
                tw.Write(" WindowsPhoneCore.lib RuntimeObject.lib PhoneAppModelHost.lib");
            }
            else if (flavor == Flavor.appcontainer)
            {
                tw.Write(" /MANIFEST:NO");
            }
            else
            {
                tw.Write(" /MANIFEST /MANIFESTUAC:\"level='asInvoker' uiAccess='false'\" /manifest:embed");
            }
            if (
                (flavor == Flavor.appcontainer) 
                && 
                (
                    (vcversion == VCVersion.v140)
                    || (vcversion == VCVersion.v141)
                    || (vcversion == VCVersion.v142)
                    )
                )
            {
                tw.Write(" WindowsApp.lib");
            }
            foreach (var s in libs)
            {
                tw.Write(" {0}", s);
            }
            foreach (var s in cfiles)
            {
                var b = Path.GetFileNameWithoutExtension(s);
                tw.Write(" obj\\{1}\\{0}.obj", b, subdir);
            }
            tw.WriteLine();
        }
        using (TextWriter tw = new StreamWriter(dest_bat))
        {
            tw.WriteLine("@echo on");
            tw.WriteLine("SETLOCAL");
            tw.WriteLine("SET VCVARSBAT=\"{0}\"", vcvarsbat);
            tw.WriteLine("SET TOOLCHAIN={0}", toolchain);
            tw.WriteLine("SET SUBDIR={0}", subdir);
            if (
                (flavor == Flavor.appcontainer) 
                && 
                (
                    (vcversion == VCVersion.v140) 
                    || (vcversion == VCVersion.v141)
                    || (vcversion == VCVersion.v142)
                    )
                )
            {
                tw.WriteLine("call %VCVARSBAT% %TOOLCHAIN% store");
            }
            else
            {
                tw.WriteLine("call %VCVARSBAT% %TOOLCHAIN%");
            }
            tw.WriteLine("@echo on");
            tw.WriteLine("mkdir .\\obj\\%SUBDIR%");
            tw.WriteLine("mkdir .\\bin\\%SUBDIR%");
            foreach (var s in cfiles)
            {
                tw.Write("CL.exe");
                tw.Write(" /nologo");
                tw.Write(" /c");
                //tw.Write(" /Zi");
                tw.Write(" /guard:cf");
                tw.Write(" /W1");
                tw.Write(" /WX-");
                tw.Write(" /sdl-");
                tw.Write(" /O2");
                tw.Write(" /Oi");
                tw.Write(" /Oy-");
                foreach (var d in defines.Keys.OrderBy(q => q))
                {
                    var v = defines[d];
                    tw.Write(" /D {0}", d);
                    if (v != null)
                    {
                        tw.Write("={0}", v);
                    }
                }
                if (machine == Machine.arm)
                {
                    tw.Write(" /D _ARM_WINAPI_PARTITION_DESKTOP_SDK_AVAILABLE=1");
                }
                switch (flavor)
                {
                    case Flavor.wp80:
                    case Flavor.wp81:
                        tw.Write(" /D WINAPI_FAMILY=WINAPI_FAMILY_PHONE_APP");
                        tw.Write(" /D SQLITE_OS_WINRT");
                        tw.Write(" /D MBEDTLS_NO_PLATFORM_ENTROPY");
                        break;
                    case Flavor.appcontainer:
                        tw.Write(" /D WINAPI_FAMILY=WINAPI_FAMILY_APP");
                        tw.Write(" /D __WRL_NO_DEFAULT_LIB__");
                        tw.Write(" /D SQLITE_OS_WINRT");
                        tw.Write(" /D MBEDTLS_NO_PLATFORM_ENTROPY");
                        break;
                }
                if (flavor == Flavor.xp)
                {
                    tw.Write(" /D _USING_V110_SDK71_");
                }
                tw.Write(" /D NDEBUG");
                tw.Write(" /D _USRDLL");
                tw.Write(" /D _WINDLL");
                tw.Write(" /DEBUG:FULL");
                tw.Write(" /Gm-");
                tw.Write(" /EHsc");
                tw.Write(" {0}", crt_option);
                tw.Write(" /GS");
                tw.Write(" /Gy");
                tw.Write(" /fp:precise");
                tw.Write(" /Zc:wchar_t");
                tw.Write(" /Zc:inline");
                tw.Write(" /Zc:forScope");
                tw.Write(" /Fo\".\\obj\\%SUBDIR%\\\\\"");
                tw.Write(" /Gd"); // Cdecl
                tw.Write(" /TC");
                tw.Write(" /analyze-");
                foreach (var p in includes)
                {
                    tw.Write(" /I{0}", p);
                }
                tw.WriteLine(" {0}", s);
            }
            tw.Write("link.exe");
            tw.Write(" @{0}", dest_linkargs);
            tw.WriteLine();
            tw.WriteLine("ENDLOCAL");
        }
    }

    class win_target
    {
        public VCVersion v { get; private set; }
        public Flavor f { get; private set; }
        public Machine m { get; private set; }

        public win_target(VCVersion av, Flavor af, Machine am)
        {
            v = av;
            f = af;
            m = am;
        }

        public string basename(string libname)
        {
            var dest = string.Format("win_{0}_{1}_{2}_{3}", libname, v, f, m);
            return dest;
        }
        public string bat(string libname)
        {
            var dest = string.Format("{0}.bat", basename(libname));
            return dest;
        }
        public string linkargs(string libname)
        {
            var dest = string.Format("{0}.linkargs", basename(libname));
            return dest;
        }
        public string subdir(string libname)
        {
            var s = string.Format("{0}\\win\\{1}\\{2}\\{3}", libname, v, f, m);
            return s;
        }
    }

    class linux_target
    {
        public string target { get; private set; }

        public linux_target(string t)
        {
            target = t;
        }

        public string basename(string libname)
        {
            var dest = string.Format("linux_{0}_{1}", libname, target);
            return dest;
        }
        public string sh(string libname)
        {
            var dest = string.Format("{0}.sh", basename(libname));
            return dest;
        }
        public string gccargs(string libname)
        {
            var dest = string.Format("{0}.gccargs", basename(libname));
            return dest;
        }
        public string subdir(string libname)
        {
            var s = string.Format("{0}/linux/{1}", libname, target);
            return s;
        }
    }

    class android_target
    {
        public string target { get; private set; }

        public android_target(string t)
        {
            target = t;
        }

        public string basename(string libname)
        {
            var dest = string.Format("android_{0}_{1}", libname, target);
            return dest;
        }
        public string sh(string libname)
        {
            var dest = string.Format("{0}.sh", basename(libname));
            return dest;
        }
        public string gccargs(string libname)
        {
            var dest = string.Format("{0}.gccargs", basename(libname));
            return dest;
        }
        public string subdir(string libname)
        {
            var s = string.Format("{0}/android/{1}", libname, target);
            return s;
        }
    }

    static void write_win_multi(
        string libname,
        IList<win_target> trios,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        foreach (var t in trios)
        {
            write_win(
                libname,
                t,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        using (TextWriter tw = new StreamWriter(string.Format("win_{0}.bat", libname)))
        {
            tw.WriteLine("@echo on");
            foreach (var t in trios)
            {
                tw.WriteLine("cmd /c {0} > err_{1}.buildoutput.txt 2>&1", t.bat(libname), t.basename(libname));
            }
        }
    }

    static void write_linux_multi(
        string libname,
        string grp,
        IList<linux_target> targets,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        foreach (var t in targets)
        {
            write_linux(
                libname,
                t,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        using (TextWriter tw = new StreamWriter(string.Format("linux_{0}_{1}.sh", libname, grp)))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            foreach (var t in targets)
            {
                tw.Write("./{0} > err_{1}.buildoutput.txt 2>&1\n", t.sh(libname), t.basename(libname));
            }
        }
    }

    static void write_android_multi(
        string libname,
        IList<android_target> targets,
        IList<string> cfiles,
        Dictionary<string,string> defines,
        IList<string> includes,
        IList<string> libs
        )
    {
        foreach (var t in targets)
        {
            write_android(
                libname,
                t,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        using (TextWriter tw = new StreamWriter(string.Format("android_{0}.sh", libname)))
        {
            tw.Write("#!/bin/sh\n");
            tw.Write("set -e\n");
            tw.Write("set -x\n");
            foreach (var t in targets)
            {
                tw.Write("./{0} > err_{1}.buildoutput.txt 2>&1\n", t.sh(libname), t.basename(libname));
            }
        }
    }

    static void add_basic_sqlite3_defines(Dictionary<string,string> defines)
    {
        defines["SQLITE_ENABLE_COLUMN_METADATA"] = null;
        defines["SQLITE_ENABLE_FTS3_PARENTHESIS"] = null;
        defines["SQLITE_ENABLE_FTS4"] = null;
        defines["SQLITE_ENABLE_FTS5"] = null;
        defines["SQLITE_ENABLE_JSON1"] = null;
        defines["SQLITE_ENABLE_MATH_FUNCTIONS"] = null;
        defines["SQLITE_ENABLE_RTREE"] = null;
        defines["SQLITE_ENABLE_SNAPSHOT"] = null;
        defines["SQLITE_DEFAULT_FOREIGN_KEYS"] = "1";
    }

    static void add_win_sqlite3_defines(Dictionary<string,string> defines)
    {
        defines["SQLITE_OS_WIN"] = null;
        defines["SQLITE_WIN32_FILEMAPPING_API"] = "1";
        defines["SQLITE_API"] = "__declspec(dllexport)";
    }

    static void add_linux_sqlite3_defines(Dictionary<string,string> defines)
    {
        defines["SQLITE_OS_UNIX"] = null;
    }

    static void add_android_sqlite3_defines(Dictionary<string,string> defines)
    {
        defines["SQLITE_OS_UNIX"] = null;
    }

    static void add_wasm_sqlite3_defines(Dictionary<string, string> defines)
    {
        defines["SQLITE_OS_UNIX"] = null;
    }

    static void add_ios_sqlite3_defines(Dictionary<string,string> defines)
    {
        defines["SQLITE_OS_UNIX"] = null;
    }

    static void write_e_sqlite3(
        )
    {
        var cfiles = new string[]
        {
            "..\\sqlite3\\sqlite3.c",
            "..\\stubs\\stubs.c",
        };

        {
            var trios = new win_target[]
            {
                new win_target(VCVersion.v142, Flavor.plain, Machine.x86),
                new win_target(VCVersion.v142, Flavor.plain, Machine.x64),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm64),

                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x86),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x64),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm64),
            };

            var defines = new Dictionary<string,string>();
            add_basic_sqlite3_defines(defines);
            add_win_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };
            write_win_multi(
                "e_sqlite3",
                trios,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>();
            add_basic_sqlite3_defines(defines);
            add_linux_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets_regular = new linux_target[]
            {
                new linux_target("x64"),
                new linux_target("x86"),
            };

            var targets_cross = new linux_target[]
            {
                new linux_target("musl-x64"),
                new linux_target("musl-arm64"),
                new linux_target("musl-armhf"),
                new linux_target("arm64"),
                new linux_target("armhf"),
                new linux_target("armsf"),
                new linux_target("mips64"),
                new linux_target("s390x"),
                new linux_target("ppc64le"),
            };

            write_linux_multi(
                "e_sqlite3",
                "regular",
                targets_regular,
                cfiles,
                defines,
                includes,
                libs
                );

            write_linux_multi(
                "e_sqlite3",
                "cross",
                targets_cross,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>();
            add_basic_sqlite3_defines(defines);
            add_android_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets = new android_target[]
            {
                //new android_target("armeabi"),
                new android_target("armeabi-v7a"),
                new android_target("arm64-v8a"),
                new android_target("x86"),
                new android_target("x86_64"),
            };

#if true
            write_android_ndk_build(
                "e_sqlite3",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#else
            write_android_multi(
                "e_sqlite3",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#endif
        }

        {
            var defines = new Dictionary<string, string>();
            add_basic_sqlite3_defines(defines);
            add_wasm_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_wasm(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs);
        }

        {
            var defines = new Dictionary<string,string>();
            add_basic_sqlite3_defines(defines);
            add_ios_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_ios(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_tvos(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_dynamic(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_maccatalyst_dynamic(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_static(
                "e_sqlite3",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );
        }

    }

    static void write_e_sqlite3mc()
    {
        var cfiles = new string[]
        {
            "..\\sqlite3mc\\sqlite3.c",
        };

        {
            var trios = new win_target[]
            {
                new win_target(VCVersion.v142, Flavor.plain, Machine.x86),
                new win_target(VCVersion.v142, Flavor.plain, Machine.x64),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm64),

                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x86),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x64),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm64),
            };

            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };

            add_basic_sqlite3_defines(defines);
            add_win_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };
            write_win_multi(
                "e_sqlite3mc",
                trios,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_linux_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets_regular = new linux_target[]
            {
                new linux_target("x64"),
                new linux_target("x86"),
            };

            var targets_cross = new linux_target[]
            {
                new linux_target("musl-x64"),
                new linux_target("musl-arm64"),
                new linux_target("musl-armhf"),
                new linux_target("arm64"),
                new linux_target("armhf"),
                new linux_target("armsf"),
                new linux_target("mips64"),
                new linux_target("s390x"),
                new linux_target("ppc64le"),
            };

            write_linux_multi(
                "e_sqlite3mc",
                "regular",
                targets_regular,
                cfiles,
                defines,
                includes,
                libs
                );

            write_linux_multi(
                "e_sqlite3mc",
                "cross",
                targets_cross,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_android_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets = new android_target[]
            {
                //new android_target("armeabi"),
                new android_target("armeabi-v7a"),
                new android_target("arm64-v8a"),
                new android_target("x86"),
                new android_target("x86_64"),
            };

#if true
            write_android_ndk_build(
                "e_sqlite3mc",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#else
            write_android_multi(
                "e_sqlite3mc",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#endif
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_wasm_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_wasm(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs);
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_ios_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_ios(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_tvos(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_dynamic(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_maccatalyst_dynamic(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_static(
                "e_sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );
        }

    }

    static void write_sqlite3mc()
    {
        var cfiles = new string[]
        {
            "..\\sqlite3mc\\sqlite3.c",
        };

        {
            var trios = new win_target[]
            {
                new win_target(VCVersion.v142, Flavor.plain, Machine.x86),
                new win_target(VCVersion.v142, Flavor.plain, Machine.x64),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm),
                new win_target(VCVersion.v142, Flavor.plain, Machine.arm64),

                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x86),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.x64),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm),
                new win_target(VCVersion.v142, Flavor.appcontainer, Machine.arm64),
            };

            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };

            add_basic_sqlite3_defines(defines);
            add_win_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };
            write_win_multi(
                "sqlite3mc",
                trios,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_linux_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets_regular = new linux_target[]
            {
                new linux_target("x64"),
                new linux_target("x86"),
            };

            var targets_cross = new linux_target[]
            {
                new linux_target("musl-x64"),
                new linux_target("musl-arm64"),
                new linux_target("musl-armhf"),
                new linux_target("arm64"),
                new linux_target("armhf"),
                new linux_target("armsf"),
                new linux_target("mips64"),
                new linux_target("s390x"),
                new linux_target("ppc64le"),
            };

            write_linux_multi(
                "sqlite3mc",
                "regular",
                targets_regular,
                cfiles,
                defines,
                includes,
                libs
                );

            write_linux_multi(
                "sqlite3mc",
                "cross",
                targets_cross,
                cfiles,
                defines,
                includes,
                libs
                );
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_android_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            var targets = new android_target[]
            {
                //new android_target("armeabi"),
                new android_target("armeabi-v7a"),
                new android_target("arm64-v8a"),
                new android_target("x86"),
                new android_target("x86_64"),
            };

#if true
            write_android_ndk_build(
                "sqlite3mc",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#else
            write_android_multi(
                "sqlite3mc",
                targets,
                cfiles,
                defines,
                includes,
                libs
                );
#endif
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_wasm_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_wasm(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs);
        }

        {
            var defines = new Dictionary<string,string>
            {
                { "CODEC_TYPE", "CODEC_TYPE_CHACHA20" },
                { "SQLITE_TEMP_STORE", "2" },
                { "SQLITE_USE_URI", "1" },
                { "SQLITE_DQS", "0" },
                { "SQLITE_SECURE_DELETE", "1" },
                { "SQLITE_ENABLE_EXTFUNC", "1" },
//                { "SQLITE_ENABLE_GEOPOLY", "1" },
//                { "SQLITE_ENABLE_REGEXP", "1" },
//                { "SQLITE_ENABLE_SERIES", "1" },
//                { "SQLITE_ENABLE_SHA3", "1" },
//                { "SQLITE_ENABLE_UUID", "1" },
            };
            add_basic_sqlite3_defines(defines);
            add_ios_sqlite3_defines(defines);
            var includes = new string[]
            {
            };
            var libs = new string[]
            {
            };

            write_ios(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_tvos(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_dynamic(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_maccatalyst_dynamic(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );

            write_mac_static(
                "sqlite3mc",
                cfiles.Select(x => x.Replace("\\", "/")).ToArray(),
                defines,
                includes.Select(x => x.Replace("\\", "/")).ToArray(),
                libs
                );
        }

    }

    public static void Main()
    {
//        write_e_sqlite3();
        write_e_sqlite3mc();
        write_sqlite3mc();
    }
}
