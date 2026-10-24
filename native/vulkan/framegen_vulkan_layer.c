// C11 · framegen_vulkan_layer.c · experimental NvPresent Vulkan layer · Windows x64 · Zig cc
#define WIN32_LEAN_AND_MEAN
#define VK_NO_PROTOTYPES
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdarg.h>
#include <string.h>
#include <stdlib.h>
#include <vulkan/vulkan_core.h>

typedef void* (WINAPI *GetInstanceProc)(void*,const char*);
typedef void* (WINAPI *GetDeviceProc)(void*,const char*);
typedef unsigned char (WINAPI *NvpInitVk)(void**,void**,void*);
typedef struct LayerInterface {
    int sType; void* pNext; uint32_t loaderLayerInterfaceVersion;
    GetInstanceProc pfnGetInstanceProcAddr; GetDeviceProc pfnGetDeviceProcAddr;
    GetInstanceProc pfnGetPhysicalDeviceProcAddr;
} LayerInterface;

static HMODULE self_module;
static INIT_ONCE once=INIT_ONCE_STATIC_INIT;
static SRWLOCK log_lock=SRWLOCK_INIT;
static wchar_t log_path[32768];
static GetInstanceProc native_gipa;
static GetDeviceProc native_gdpa;
static int initialized;
static int relax_vsync;
static SRWLOCK state_lock=SRWLOCK_INIT;
static VkInstance active_instance;
typedef struct DeviceState {VkDevice device;VkPhysicalDevice physical;} DeviceState;
static DeviceState devices[8];
static PFN_vkCreateInstance next_create_instance;
static PFN_vkCreateDevice next_create_device;
static PFN_vkCreateSwapchainKHR next_create_swapchain;

static void log_line(const char* fmt,...) {
    AcquireSRWLockExclusive(&log_lock);
    FILE* stream=_wfopen(log_path,L"ab");
    if(stream) {va_list args;va_start(args,fmt);vfprintf(stream,fmt,args);va_end(args);fputc('\n',stream);fclose(stream);}
    ReleaseSRWLockExclusive(&log_lock);
}
static unsigned char* config_pointer(HMODULE module) {
    unsigned char* base=(unsigned char*)module;
    IMAGE_DOS_HEADER* dos=(IMAGE_DOS_HEADER*)base;
    if(dos->e_magic!=IMAGE_DOS_SIGNATURE) return NULL;
    IMAGE_NT_HEADERS64* nt=(IMAGE_NT_HEADERS64*)(base+dos->e_lfanew);
    if(nt->Signature!=IMAGE_NT_SIGNATURE) return NULL;
    unsigned char* init=(unsigned char*)GetProcAddress(module,"NVP_Init_D3D");
    if(!init) return NULL;
    for(int n=0;n<89;n++) if(init[n]==0x48&&init[n+1]==0x8d&&init[n+2]==0x0d) {
        int32_t displacement;memcpy(&displacement,init+n+3,4);
        unsigned char* target=init+n+7+displacement;
        if(target<base||target+0x12a6>=base+nt->OptionalHeader.SizeOfImage) continue;
        IMAGE_SECTION_HEADER* sections=IMAGE_FIRST_SECTION(nt);
        for(unsigned int i=0;i<nt->FileHeader.NumberOfSections;i++)
            if((sections[i].Characteristics&IMAGE_SCN_MEM_WRITE)&&target>=base+sections[i].VirtualAddress&&target+0x12a6<=base+sections[i].VirtualAddress+sections[i].Misc.VirtualSize) return target;
    }
    return NULL;
}
static BOOL CALLBACK initialize(PINIT_ONCE init_once,PVOID parameter,PVOID* context) {
    (void)init_once;(void)parameter;(void)context;
    wchar_t directory[32768],backend_path[32768];
    DWORD count=GetModuleFileNameW(self_module,directory,32768);
    if(!count||count>=32760) return TRUE;
    wchar_t* slash=wcsrchr(directory,L'\\');if(!slash)return TRUE;slash[1]=0;
    _snwprintf(log_path,32768,L"%lsFramegenVulkan.log",directory);
    _snwprintf(backend_path,32768,L"%lsNVSmoothBackend.dll",directory);
    char option[8]={0};
    GetEnvironmentVariableA("SM86_VK_RELAX_VSYNC",option,sizeof(option));relax_vsync=!strcmp(option,"1");
    log_line("EXPERIMENTAL Vulkan layer v2 initialization; relax_vsync=%d; no global settings changed.",relax_vsync);
    const char* keys[]={"SM86_ENABLE_D3D11_BRIDGE","SM86_ENABLE_OSD","SM86_FORCE_VSYNC","SM86_BASE_FPS_CAP","SM86_HALF_REFRESH_CAP","SM86_LOW_LATENCY"};
    for(unsigned int i=0;i<sizeof(keys)/sizeof(keys[0]);i++) SetEnvironmentVariableA(keys[i],"0");
    SetEnvironmentVariableA("SM86_DIAGNOSTICS","1");
    HMODULE backend=LoadLibraryW(backend_path);
    if(!backend) {log_line("NVSmooth backend load failed: %lu",GetLastError());return TRUE;}
    HMODULE nvp=NULL;unsigned char* cfg=NULL;
    for(int n=0;n<200;n++) {
        nvp=GetModuleHandleW(L"NvPresent64.dll");if(nvp)cfg=config_pointer(nvp);
        if(cfg&&cfg[0x12a5]&&cfg[0x4c]&&cfg[0xe9]) break;
        Sleep(50);
    }
    if(!cfg||!cfg[0x12a5]||!cfg[0x4c]||!cfg[0xe9]) {log_line("NVSmooth initialization incomplete; Vulkan integration unavailable.");return TRUE;}
    NvpInitVk init=(NvpInitVk)GetProcAddress(nvp,"NVP_Init_Vulkan");
    static const unsigned char expected[]={0x48,0x89,0x5c,0x24,0x08,0x48,0x89,0x74,0x24,0x10,0x57,0x48,0x83,0xec,0x20};
    if(!init||memcmp((void*)init,expected,sizeof(expected))) {log_line("Unrecognized Vulkan entry; no Vulkan changes applied.");return TRUE;}
    log_line("Before Vulkan initialization: D3D=%u Vulkan=%u capability=%u initialized=%u",cfg[0x4c],cfg[0x4e],cfg[0xe9],cfg[0x12a5]);
    cfg[0x4e]=1;
    unsigned char result=init((void**)&native_gipa,(void**)&native_gdpa,NULL);
    initialized=result&&native_gipa&&native_gdpa;
    log_line("NVP_Init_Vulkan=%u GIPA=%p GDPA=%p flag=%u",result,(void*)native_gipa,(void*)native_gdpa,cfg[0x4e]);
    return TRUE;
}
static VkResult VKAPI_CALL layer_create_instance(const VkInstanceCreateInfo* info,const VkAllocationCallbacks* allocator,VkInstance* instance) {
    VkResult result=next_create_instance(info,allocator,instance);
    if(result==VK_SUCCESS){AcquireSRWLockExclusive(&state_lock);active_instance=*instance;ReleaseSRWLockExclusive(&state_lock);}
    log_line("vkCreateInstance result=%d",result);return result;
}
static VkResult VKAPI_CALL layer_create_device(VkPhysicalDevice physical,const VkDeviceCreateInfo* info,const VkAllocationCallbacks* allocator,VkDevice* device) {
    VkResult result=next_create_device(physical,info,allocator,device);
    if(result==VK_SUCCESS){
        AcquireSRWLockExclusive(&state_lock);
        for(unsigned n=0;n<8;n++)if(!devices[n].device){devices[n].device=*device;devices[n].physical=physical;break;}
        ReleaseSRWLockExclusive(&state_lock);
    }
    log_line("vkCreateDevice result=%d",result);return result;
}
static VkResult VKAPI_CALL layer_create_swapchain(VkDevice device,const VkSwapchainCreateInfoKHR* info,const VkAllocationCallbacks* allocator,VkSwapchainKHR* swapchain) {
    if(!info)return VK_ERROR_INITIALIZATION_FAILED;
    VkSwapchainCreateInfoKHR revised=*info;
    VkInstance instance;VkPhysicalDevice physical=NULL;
    AcquireSRWLockShared(&state_lock);instance=active_instance;
    for(unsigned n=0;n<8;n++)if(devices[n].device==device){physical=devices[n].physical;break;}
    ReleaseSRWLockShared(&state_lock);
    // Only ordinary swapchains without extension chains are eligible for this narrow test.
    // An unknown chain might declare a different set of compatible present modes.
    if(relax_vsync&&physical&&instance&&!info->pNext&&
       (info->presentMode==VK_PRESENT_MODE_FIFO_KHR||info->presentMode==VK_PRESENT_MODE_FIFO_RELAXED_KHR)){
        PFN_vkGetPhysicalDeviceSurfacePresentModesKHR query=(PFN_vkGetPhysicalDeviceSurfacePresentModesKHR)native_gipa(instance,"vkGetPhysicalDeviceSurfacePresentModesKHR");
        uint32_t count=0;
        if(query&&query(physical,info->surface,&count,NULL)==VK_SUCCESS&&count&&count<=64){
            VkPresentModeKHR modes[64];VkResult qr=query(physical,info->surface,&count,modes);
            if(qr==VK_SUCCESS)for(uint32_t n=0;n<count;n++)if(modes[n]==VK_PRESENT_MODE_IMMEDIATE_KHR){revised.presentMode=VK_PRESENT_MODE_IMMEDIATE_KHR;break;}
        }
    }
    log_line("vkCreateSwapchainKHR: requested_mode=%d selected_mode=%d pNext=%p size=%ux%u images=%u relax=%d",info->presentMode,revised.presentMode,info->pNext,info->imageExtent.width,info->imageExtent.height,info->minImageCount,relax_vsync);
    VkResult result=next_create_swapchain(device,&revised,allocator,swapchain);
    log_line("vkCreateSwapchainKHR result=%d",result);return result;
}
static void* intercept(const char* name,void* result){
    if(!result)return NULL;
    AcquireSRWLockExclusive(&state_lock);
    if(!strcmp(name,"vkCreateInstance")){
        if(!next_create_instance)next_create_instance=(PFN_vkCreateInstance)result;
        if((void*)next_create_instance==result)result=(void*)&layer_create_instance;
    }else if(!strcmp(name,"vkCreateDevice")){
        if(!next_create_device)next_create_device=(PFN_vkCreateDevice)result;
        if((void*)next_create_device==result)result=(void*)&layer_create_device;
    }else if(!strcmp(name,"vkCreateSwapchainKHR")){
        if(!next_create_swapchain)next_create_swapchain=(PFN_vkCreateSwapchainKHR)result;
        if((void*)next_create_swapchain==result)result=(void*)&layer_create_swapchain;
    }
    ReleaseSRWLockExclusive(&state_lock);return result;
}
__declspec(dllexport) void* WINAPI vkGetInstanceProcAddr(void* instance,const char* name);
__declspec(dllexport) void* WINAPI vkGetDeviceProcAddr(void* device,const char* name);
__declspec(dllexport) void* WINAPI vkGetInstanceProcAddr(void* instance,const char* name) {
    InitOnceExecuteOnce(&once,initialize,NULL,NULL);
    if(!initialized||!name)return NULL;
    if(!strcmp(name,"vkGetInstanceProcAddr"))return (void*)&vkGetInstanceProcAddr;
    if(!strcmp(name,"vkGetDeviceProcAddr"))return (void*)&vkGetDeviceProcAddr;
    if(!instance&&strcmp(name,"vkCreateInstance"))return NULL;
    void* result=native_gipa(instance,name);
    if(!strcmp(name,"vkCreateInstance")||!strcmp(name,"vkCreateDevice")) log_line("Forwarding %s -> %p",name,result);
    return intercept(name,result);
}
__declspec(dllexport) void* WINAPI vkGetDeviceProcAddr(void* device,const char* name) {
    if(!initialized||!device||!name)return NULL;
    void* result=native_gdpa(device,name);
    if(!strcmp(name,"vkCreateSwapchainKHR")||!strcmp(name,"vkQueuePresentKHR"))log_line("Forwarding %s -> %p",name,result);
    return intercept(name,result);
}
__declspec(dllexport) int WINAPI vkNegotiateLoaderLayerInterfaceVersion(LayerInterface* interface) {
    if(!interface||interface->loaderLayerInterfaceVersion<1)return -3;
    interface->loaderLayerInterfaceVersion=1;
    interface->pfnGetInstanceProcAddr=vkGetInstanceProcAddr;
    interface->pfnGetDeviceProcAddr=vkGetDeviceProcAddr;
    interface->pfnGetPhysicalDeviceProcAddr=NULL;
    return 0;
}
BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,LPVOID reserved) {
    (void)reserved;
    if(reason==DLL_PROCESS_ATTACH) {self_module=module;DisableThreadLibraryCalls(module);}
    return TRUE;
}
