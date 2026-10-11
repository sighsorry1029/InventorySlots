// Isolated managed checks on the original Windows x64 Valheim Mono runtime.
// This host does not start Unity, BepInEx's preloader, or the game.
#include <windows.h>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <vector>

static_assert(sizeof(void*) == 8, "Build MonoHost with the x64 toolchain.");
struct MonoDomain;
struct MonoAssembly;

template<typename T> static T Symbol(HMODULE module, const char* name)
{
    FARPROC address = GetProcAddress(module, name);
    if (!address) throw std::runtime_error(std::string("Missing Mono export: ") + name);
    return reinterpret_cast<T>(address);
}

static std::string Utf8(const wchar_t* value)
{
    int size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value, -1, nullptr, 0, nullptr, nullptr);
    if (!size) throw std::runtime_error("Invalid UTF-16 argument.");
    std::string result(size, '\0');
    if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value, -1, &result[0], size, nullptr, nullptr))
        throw std::runtime_error("Could not encode argument as UTF-8.");
    result.resize(size - 1);
    return result;
}

int wmain(int argc, wchar_t** argv)
{
    if (argc < 5)
    {
        std::fprintf(stderr, "Usage: MonoHost <absolute runtime.dll> <Managed> <Mono etc> <test.exe> [test arguments]\n");
        return 2;
    }
    HMODULE module = LoadLibraryExW(argv[1], nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!module)
    {
        std::fprintf(stderr, "Cannot load x64 Mono runtime (Windows error %lu).\n", GetLastError());
        return 2;
    }
    try
    {
        auto setDirs = Symbol<void(__cdecl*)(const char*, const char*)>(module, "mono_set_dirs");
        auto setAssemblies = Symbol<void(__cdecl*)(const char*)>(module, "mono_set_assemblies_path");
        auto config = Symbol<void(__cdecl*)(const char*)>(module, "mono_config_parse");
        auto init = Symbol<MonoDomain*(__cdecl*)(const char*, const char*)>(module, "mono_jit_init_version");
        auto open = Symbol<MonoAssembly*(__cdecl*)(MonoDomain*, const char*)>(module, "mono_domain_assembly_open");
        auto exec = Symbol<int(__cdecl*)(MonoDomain*, MonoAssembly*, int, char**)>(module, "mono_jit_exec");
        auto cleanup = Symbol<void(__cdecl*)(MonoDomain*)>(module, "mono_jit_cleanup");

        std::string managed = Utf8(argv[2]), etc = Utf8(argv[3]);
        std::vector<std::string> arguments;
        for (int i = 4; i < argc; ++i) arguments.push_back(Utf8(argv[i]));
        std::vector<char*> pointers;
        for (auto& argument : arguments) pointers.push_back(&argument[0]);
        pointers.push_back(nullptr);

        // The player ships mscorlib in Managed, not MonoBleedingEdge/lib.
        setDirs(managed.c_str(), etc.c_str());
        setAssemblies(managed.c_str());
        config(nullptr);
        MonoDomain* domain = init("InventoryActionsSmoke", "v4.0.30319");
        if (!domain) throw std::runtime_error("Mono initialization failed.");
        MonoAssembly* assembly = open(domain, arguments[0].c_str());
        if (!assembly)
        {
            std::fprintf(stderr, "Cannot open test assembly.\n");
            cleanup(domain);
            return 2;
        }
        int result = exec(domain, assembly, argc - 4, pointers.data());
        std::printf("Managed exit code: %d; cleaning up Mono.\n", result);
        std::fflush(stdout);
        cleanup(domain);
        std::printf("Mono cleanup completed.\n");
        // Do not force success or skip shutdown. Native crashes remain failures.
        // The process owns the runtime module until OS teardown.
        return result;
    }
    catch (const std::exception& error)
    {
        std::fprintf(stderr, "%s\n", error.what());
        return 2;
    }
}
