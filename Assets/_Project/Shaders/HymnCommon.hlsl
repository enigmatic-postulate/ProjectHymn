// Project Hymn: shared noise for the hand-written shaders (Starlit Spring). Plain HLSL, no pipeline includes.
#ifndef HYMN_COMMON_INCLUDED
#define HYMN_COMMON_INCLUDED

// Pulses once a beat (1 on the beat, falling to 0); set by the AmbientBeat component.
float _HymnBeat;

float HymnHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float HymnNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = HymnHash21(i);
    float b = HymnHash21(i + float2(1.0, 0.0));
    float c = HymnHash21(i + float2(0.0, 1.0));
    float d = HymnHash21(i + float2(1.0, 1.0));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float HymnFbm(float2 p)
{
    float s = 0.0;
    float a = 0.5;
    for (int k = 0; k < 5; k++)
    {
        s += a * HymnNoise(p);
        p = p * 2.03 + float2(17.1, 9.2);
        a *= 0.5;
    }
    return s;
}

// A field of twinkling points: cells of 1/scale, about (1 - density) of them lit.
float HymnStars(float2 uv, float scale, float density, float t)
{
    float2 g = uv * scale;
    float2 id = floor(g);
    float2 f = frac(g) - 0.5;
    float h = HymnHash21(id);
    float lit = step(1.0 - density, h);
    float2 off = (float2(HymnHash21(id + 7.13), HymnHash21(id + 3.71)) - 0.5) * 0.7;
    float d = length(f - off);
    float size = 0.035 + 0.075 * frac(h * 13.7);
    float twinkle = 0.55 + 0.45 * sin(t * (1.3 + 3.0 * frac(h * 7.1)) + h * 40.0);
    float core = smoothstep(size, 0.0, d);
    float glow = smoothstep(size * 3.5, 0.0, d) * 0.25;
    return lit * (core + glow) * twinkle * (0.4 + 0.6 * frac(h * 31.3));
}

#endif
