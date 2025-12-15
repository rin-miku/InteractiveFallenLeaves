int RenderResolution;
int SimulationResolution;
float DeltaTime;
float GridScale;

int CoordToIndex(int2 coord)
{
    return clamp(coord.x, 0, SimulationResolution - 1) + clamp(coord.y, 0, SimulationResolution - 1) * SimulationResolution;
}

float4 BilinearInterpolation(StructuredBuffer<float4> buffer, float2 coord)
{
    coord = clamp(coord, 0.0, SimulationResolution - 1);

    int2 p0 = int2(floor(coord));
    int2 p1 = min(p0 + 1, SimulationResolution - 1);

    float2 f = coord - p0;

    float4 a = buffer[p0.x + p0.y * SimulationResolution];
    float4 b = buffer[p1.x + p0.y * SimulationResolution];
    float4 c = buffer[p0.x + p1.y * SimulationResolution];
    float4 d = buffer[p1.x + p1.y * SimulationResolution];

    float4 ab = lerp(a, b, f.x);
    float4 cd = lerp(c, d, f.x);

    return lerp(ab, cd, f.y);
}

float4 Gradient(StructuredBuffer<float4> buffer, float cellSize, int2 coord)
{
    float left = buffer[CoordToIndex(coord - int2(1, 0))].x;
    float right = buffer[CoordToIndex(coord + int2(1, 0))].x;
    float bottom = buffer[CoordToIndex(coord - int2(0, 1))].x;
    float top = buffer[CoordToIndex(coord + int2(0, 1))].x;

    return float4(right - left, top - bottom, 0.0, 0.0) / cellSize;
}