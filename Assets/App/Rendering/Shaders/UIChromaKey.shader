// Chroma-key shader for UI RawImage reaction clips. Keys out a solid background color so a
// clip's subject composites onto the live camera feed. Opt-in per meme (BackgroundBlend =
// ChromaKey); if this shader is missing the renderer falls back to the default UI material.
Shader "MemeAR/UIChromaKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyColor ("Key Color", Color) = (0,1,0,1)
        _KeyThreshold ("Key Threshold", Range(0,1)) = 0.35
        _KeySmooth ("Key Smoothness", Range(0,1)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _KeyColor;
            float _KeyThreshold;
            float _KeySmooth;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                // Distance from the key color in RGB; near the key -> transparent.
                float dist = distance(tex.rgb, _KeyColor.rgb);
                float a = smoothstep(_KeyThreshold - _KeySmooth, _KeyThreshold + _KeySmooth, dist);
                fixed4 outCol = tex * IN.color;
                outCol.a *= a;
                return outCol;
            }
            ENDCG
        }
    }
}
