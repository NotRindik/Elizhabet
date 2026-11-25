#ifndef PIXEL_BLEND_INCLUDED
#define PIXEL_BLEND_INCLUDED

float BlendScreen(float b, float s) { return b + s - b * s; }
float BlendColorBurn(float b, float s) { return b >= 1.0 ? 1.0 : (s <= 0.0 ? 0.0 : 1.0 - min(1.0, (1.0 - b) / s)); }
float BlendColorDodge(float b, float s) { return b <= 0.0 ? 0.0 : (s >= 1.0 ? 1.0 : min(1.0, b / (1.0 - s))); }
float BlendHard(float b, float s) { return s <= 0.5 ? b * 2.0 * s : BlendScreen(b, 2.0 * s - 1.0); }

float BlendSoft(float b, float s)
{
    if(s <= 0.5) return b - (1.0 - 2.0 * s) * b * (1.0 - b);
    float d = b <= 0.25 ? ((16.0 * b - 12.0) * b + 4.0) * b : sqrt(b);
    return b + (2.0 * s - 1.0) * (d - b);
}

float BlendChannel(int mode, float b, float s)
{
    float r = s;
    switch(mode)
    {
        case 1: r = min(b, s); break;
        case 2: r = b * s; break;
        case 3: r = BlendColorBurn(b, s); break;
        case 4: r = max(0.0, b + s - 1.0); break;
        case 5: r = max(0.0, b - s); break;
        case 6: r = max(b, s); break;
        case 7: r = BlendScreen(b, s); break;
        case 8:
        case 9: r = BlendColorDodge(b, s); break;
        case 10:
        case 11: r = min(1.0, b + s); break;
        case 12: r = BlendHard(s, b); break;
        case 13: r = BlendSoft(b, s); break;
        case 14: r = BlendHard(b, s); break;
        case 15: r = abs(b - s); break;
        case 16: r = s <= 0.5 ? BlendColorBurn(b, 2.0 * s) : BlendColorDodge(b, 2.0 * s - 1.0); break;
        case 17: r = saturate(b + 2.0 * s - 1.0); break;
        case 18: r = s <= 0.5 ? min(b, 2.0 * s) : max(b, 2.0 * s - 1.0); break;
        case 19: r = b + s >= 1.0 ? 1.0 : 0.0; break;
        case 20: r = b + s - 2.0 * b * s; break;
        case 23: r = s <= 0.0 ? 1.0 : min(1.0, b / s); break;
    }
    return r;
}

float BlendLum(float3 c) { return 0.3 * c.x + 0.59 * c.y + 0.11 * c.z; }

float3 BlendSetLum(float3 c, float l)
{
    c += l - BlendLum(c);
    float lum = BlendLum(c);
    float n = min(c.x, min(c.y, c.z));
    float x = max(c.x, max(c.y, c.z));
    if(n < 0.0) c = lum + (c - lum) * (lum / max(0.0001, lum - n));
    if(x > 1.0) c = lum + (c - lum) * ((1.0 - lum) / max(0.0001, x - lum));
    return c;
}

float BlendSat(float3 c) { return max(c.x, max(c.y, c.z)) - min(c.x, min(c.y, c.z)); }

float3 BlendSetSat(float3 c, float s)
{
    float mx = max(c.x, max(c.y, c.z));
    float mn = min(c.x, min(c.y, c.z));
    float range = mx - mn;
    if(range <= 0.0001) return float3(0, 0, 0);
    return (c - mn) / range * s;
}

float3 BlendColor(int mode, float3 b, float3 s)
{
    if(mode == 21) return BlendLum(s) < BlendLum(b) ? s : b;
    if(mode == 22) return BlendLum(s) > BlendLum(b) ? s : b;
    if(mode == 24) return BlendSetLum(BlendSetSat(s, BlendSat(b)), BlendLum(b));
    if(mode == 25) return BlendSetLum(BlendSetSat(b, BlendSat(s)), BlendLum(b));
    if(mode == 26) return BlendSetLum(s, BlendLum(b));
    if(mode == 27) return BlendSetLum(b, BlendLum(s));
    return float3(BlendChannel(mode, b.x, s.x), BlendChannel(mode, b.y, s.y), BlendChannel(mode, b.z, s.z));
}

float3 PixelBlendLinear(int mode, float3 backLinear, float3 srcLinear)
{
    float3 b = backLinear;
    float3 s = srcLinear;
    #if !defined(UNITY_COLORSPACE_GAMMA)
        b = LinearToSRGB(b);
        s = LinearToSRGB(s);
    #endif
    float3 mixed = saturate(BlendColor(mode, b, s));
    #if !defined(UNITY_COLORSPACE_GAMMA)
        mixed = SRGBToLinear(mixed);
    #endif
    return mixed;
}

#endif
