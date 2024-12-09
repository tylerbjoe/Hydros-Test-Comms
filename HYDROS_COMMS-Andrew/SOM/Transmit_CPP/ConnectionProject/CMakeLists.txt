cmake_minimum_required(VERSION 3.15)
project(TcpClientExample)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)
set(FFTW_INCLUDE_DIRS "/usr/include")
set(FFTW_LIBRARIES "/usr/lib/x86_64-linux-gnu/libfftw3.so")

include_directories("/mnt/c/Users/AEngel/Documents/Github/HYDROS_COMMS/SOM/Transmit_CPP/ConnectionProject/build/CMakeFiles/TcpClientExample.dir")
include_directories(${FFTW_INCLUDE_DIRS})

# Specify the toolchain file provided by vcpkg
set(CMAKE_TOOLCHAIN_FILE "/mnt/c/Users/AEngel/Documents/Github/HYDROS_COMMS/SOM/Transmit_CPP/vcpkg/scripts/buildsystems/vcpkg.cmake")

find_package(Boost REQUIRED COMPONENTS system)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(OpenMP REQUIRED)

add_executable(TcpClientExample main.cpp)

link_directories(${FFTW_LIBRARIES})
target_link_libraries(TcpClientExample Boost::system nlohmann_json::nlohmann_json OpenMP::OpenMP_CXX ${FFTW_LIBRARIES})


