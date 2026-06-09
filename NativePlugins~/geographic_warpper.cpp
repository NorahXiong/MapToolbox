#include <GeographicLib/UTMUPS.hpp>

extern "C" {
    void UTMUPS_Forward(double lat, double lon, int* zone, bool* northp, double* x, double* y) {
        GeographicLib::UTMUPS::Forward(lat, lon, *zone, *northp, *x, *y);
    }
}
