#include <algorithm>
#include <cmath>
#include <cstdint>
extern "C" void Raster(int size, int triangleCount, const float* vertices, const int* indices,
                       const uint8_t* colors, float* depths, uint8_t* output) {
    for (int triangle = 0; triangle < triangleCount; ++triangle) {
        const float* a = vertices + 3 * indices[3 * triangle];
        const float* b = vertices + 3 * indices[3 * triangle + 1];
        const float* c = vertices + 3 * indices[3 * triangle + 2];
        const float denominator = (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
        if (std::abs(denominator) < 0.00001f) continue;
        const int xmin = std::max(0, (int)std::floor(std::min({a[0],b[0],c[0]})));
        const int xmax = std::min(size-1, (int)std::ceil(std::max({a[0],b[0],c[0]})));
        const int ymin = std::max(0, (int)std::floor(std::min({a[1],b[1],c[1]})));
        const int ymax = std::min(size-1, (int)std::ceil(std::max({a[1],b[1],c[1]})));
        for (int y = ymin; y <= ymax; ++y) for (int x = xmin; x <= xmax; ++x) {
            const float dx=x+0.5f-a[0], dy=y+0.5f-a[1];
            const float u=(dx*(c[1]-a[1])-dy*(c[0]-a[0]))/denominator;
            const float v=((b[0]-a[0])*dy-(b[1]-a[1])*dx)/denominator;
            if (u<0 || v<0 || u+v>1) continue;
            const float depth=a[2]+u*(b[2]-a[2])+v*(c[2]-a[2]);
            int pixel=y*size+x;
            if (depth>depths[pixel]) {
                depths[pixel]=depth;
                std::copy_n(colors + 3 * triangle, 3, output + 3 * pixel);
            }
        }
    }
}
