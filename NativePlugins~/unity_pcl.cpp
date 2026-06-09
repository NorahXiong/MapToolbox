#include <pcl/io/pcd_io.h>
#include <pcl/point_types.h>
#include <pcl/PCLPointCloud2.h>
#include <pcl/conversions.h>
#include <cstdint>
#include <cstring>
#include <vector>

namespace unity_pcl {
    static pcl::PCLPointCloud2 cloud2;
    static std::vector<pcl::PointXYZRGBA> buf_xyzrgba;
    static std::vector<pcl::PointXYZI> buf_xyzi;
    static std::vector<pcl::PointXYZ> buf_xyz;

    int load(const char* file) {
        buf_xyzrgba.clear();
        buf_xyzi.clear();
        buf_xyz.clear();
        if (pcl::io::loadPCDFile(file, cloud2) == -1)
            return -1;
        return 0;
    }

    uint32_t height() { return cloud2.height; }
    uint32_t width() { return cloud2.width; }
    uint8_t is_bigendian() { return cloud2.is_bigendian ? 1 : 0; }
    uint32_t point_step() { return cloud2.point_step; }
    uint32_t row_step() { return cloud2.row_step; }
    uint8_t is_dense() { return cloud2.is_dense ? 1 : 0; }
    void* data_blob() { return cloud2.data.data(); }
    uint64_t size_blob() { return cloud2.data.size(); }

    void clear() {
        cloud2.data.clear();
        cloud2.fields.clear();
        cloud2.height = 0;
        cloud2.width = 0;
        buf_xyzrgba.clear();
        buf_xyzi.clear();
        buf_xyz.clear();
    }

    bool contains_field(const char* name) {
        for (const auto& f : cloud2.fields)
            if (f.name == name) return true;
        return false;
    }

    bool contains_xyz() {
        return contains_field("x") && contains_field("y") && contains_field("z");
    }
    bool contains_i() { return contains_field("intensity"); }
    bool contains_rgb() { return contains_field("rgb"); }
    bool contains_rgba() { return contains_field("rgba"); }

    uint64_t load_as_xyzrgba(pcl::PointXYZRGBA*& data) {
        pcl::PointCloud<pcl::PointXYZRGBA> cloud;
        pcl::fromPCLPointCloud2(cloud2, cloud);
        buf_xyzrgba.assign(cloud.points.begin(), cloud.points.end());
        data = buf_xyzrgba.data();
        return buf_xyzrgba.size();
    }

    uint64_t load_as_xyzi(pcl::PointXYZI*& data) {
        pcl::PointCloud<pcl::PointXYZI> cloud;
        pcl::fromPCLPointCloud2(cloud2, cloud);
        buf_xyzi.assign(cloud.points.begin(), cloud.points.end());
        data = buf_xyzi.data();
        return buf_xyzi.size();
    }

    uint64_t load_as_xyz(pcl::PointXYZ*& data) {
        pcl::PointCloud<pcl::PointXYZ> cloud;
        pcl::fromPCLPointCloud2(cloud2, cloud);
        buf_xyz.assign(cloud.points.begin(), cloud.points.end());
        data = buf_xyz.data();
        return buf_xyz.size();
    }
}

extern "C" {
    int load(const char* file) { return unity_pcl::load(file); }
    uint32_t height() { return unity_pcl::height(); }
    uint32_t width() { return unity_pcl::width(); }
    uint8_t is_bigendian() { return unity_pcl::is_bigendian(); }
    uint32_t point_step() { return unity_pcl::point_step(); }
    uint32_t row_step() { return unity_pcl::row_step(); }
    uint8_t is_dense() { return unity_pcl::is_dense(); }
    void* data_blob() { return unity_pcl::data_blob(); }
    uint64_t size_blob() { return unity_pcl::size_blob(); }
    void clear() { unity_pcl::clear(); }
    bool contains_field(const char* name) { return unity_pcl::contains_field(name); }
    bool contains_xyz() { return unity_pcl::contains_xyz(); }
    bool contains_i() { return unity_pcl::contains_i(); }
    bool contains_rgb() { return unity_pcl::contains_rgb(); }
    bool contains_rgba() { return unity_pcl::contains_rgba(); }
    uint64_t load_as_xyzrgba(void** data) {
        pcl::PointXYZRGBA* p = nullptr;
        auto n = unity_pcl::load_as_xyzrgba(p);
        *data = p;
        return n;
    }
    uint64_t load_as_xyzi(void** data) {
        pcl::PointXYZI* p = nullptr;
        auto n = unity_pcl::load_as_xyzi(p);
        *data = p;
        return n;
    }
    uint64_t load_as_xyz(void** data) {
        pcl::PointXYZ* p = nullptr;
        auto n = unity_pcl::load_as_xyz(p);
        *data = p;
        return n;
    }
}
