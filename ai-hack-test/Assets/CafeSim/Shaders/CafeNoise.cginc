#ifndef CAFE_NOISE_INCLUDED
#define CAFE_NOISE_INCLUDED

// Small hash / value-noise helpers shared by the receipt shaders.
float CafeHash (float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float CafeNoise (float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = CafeHash(i);
    float b = CafeHash(i + float2(1, 0));
    float c = CafeHash(i + float2(0, 1));
    float d = CafeHash(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float CafeFbm (float2 p)
{
    float v = 0.0;
    float amplitude = 0.5;
    for (int k = 0; k < 4; k++)
    {
        v += amplitude * CafeNoise(p);
        p = p * 2.03 + 17.0;
        amplitude *= 0.5;
    }
    return v;
}

#endif
