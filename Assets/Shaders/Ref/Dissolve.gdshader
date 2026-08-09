shader_type canvas_item;
uniform vec4 ColorParameter : source_color = vec4(0.790780, 0.889110, 0.998456, 1.000000);
uniform float FloatParameter5 = 0.005;
uniform float FloatParameter2 = 25;
uniform sampler2D  PerlinNoise;
uniform float FloatParameter = 15;



void fragment() {

vec4 color_ = texture(TEXTURE, UV);
float isTransparency = step(color_.a, 0);

// ColorParameter:16
	vec4 n_out16p0 = ColorParameter;
// Input:51
	vec2 n_out51p0 = UV;
	float n_out51p2 = n_out51p0.g;
// FloatParameter:71
	float n_out71p0 = FloatParameter5;
// FloatOp:56
	float n_out56p0 = n_out51p2 + n_out71p0;
// FloatParameter:19
	float n_out19p0 = FloatParameter2;
// FloatOp:53
	float n_out53p0 = n_out56p0 * n_out19p0;
// FloatFunc:54
	float n_out54p0 = ceil(n_out53p0);
// FloatOp:55
	float n_out55p0 = n_out54p0 / n_out19p0;
// Input:46
	vec2 n_out46p0 = UV;
	float n_out46p1 = n_out46p0.r;
// FloatOp:57
	float n_out57p0 = n_out46p1 + n_out71p0;
// FloatOp:48
	float n_out48p0 = n_out57p0 * n_out19p0;
// FloatFunc:49
	float n_out49p0 = ceil(n_out48p0);
// FloatOp:50
	float n_out50p0 = n_out49p0 / n_out19p0;
// VectorCompose:58
	vec2 n_out58p0 = vec2(n_out55p0, n_out50p0);
// Input:60
	vec2 n_out60p0 = UV;
	float n_out60p2 = n_out60p0.g;
// FloatOp:65
	float n_out65p0 = n_out60p2 * n_out19p0;
// FloatFunc:66
	float n_out66p0 = ceil(n_out65p0);
// FloatOp:69
	float n_out69p0 = n_out66p0 / n_out19p0;
// Input:61
	vec2 n_out61p0 = UV;
	float n_out61p1 = n_out61p0.r;
// FloatOp:62
	float n_out62p0 = n_out61p1 * n_out19p0;
// FloatFunc:63
	float n_out63p0 = ceil(n_out62p0);
// FloatOp:64
	float n_out64p0 = n_out63p0 / n_out19p0;
// VectorCompose:67
	vec2 n_out67p0 = vec2(n_out69p0, n_out64p0);
// VectorOp:59
	vec2 n_out59p0 = n_out58p0 - n_out67p0;
// VectorOp:73
	vec2 n_in73p1 = vec2(1.50000, 1.50000);
	vec2 n_out73p0 = n_out59p0 * n_in73p1;
// Input:2
	vec2 n_out2p0 = UV;
	float n_out2p1 = n_out2p0.r;
// FloatOp:18
	float n_out18p0 = n_out2p1 * n_out19p0;
// FloatFunc:5
	float n_out5p0 = ceil(n_out18p0);
// FloatOp:4
	float n_out4p0 = n_out5p0 / n_out19p0;
// Input:20
	vec2 n_out20p0 = UV;
	float n_out20p2 = n_out20p0.g;
// FloatOp:24
	float n_out24p0 = n_out20p2 * n_out19p0;
// FloatFunc:21
	float n_out21p0 = ceil(n_out24p0);
// FloatOp:23
	float n_out23p0 = n_out21p0 / n_out19p0;
// VectorCompose:45
	vec2 n_out45p0 = vec2(n_out4p0, n_out23p0);
// VectorOp:70
	vec2 n_out70p0 = n_out73p0 + n_out45p0;
// Texture2D:7
	vec4 n_out7p0 = texture( PerlinNoise, n_out70p0);
	float n_out7p1 = n_out7p0.r;
// Input:27
	vec2 n_out27p0 = UV;
	float n_out27p2 = n_out27p0.g;
// FloatOp:79
	float n_in79p1 = 0.0;
	float n_out79p0 = n_out27p2 + n_in79p1;
// FloatOp:30
	float n_out30p0 = n_out79p0 * n_out19p0;
// FloatFunc:31
	float n_out31p0 = ceil(n_out30p0);
// FloatOp:32
	float n_out32p0 = n_out31p0 / n_out19p0;
// Input:10
	float n_out10p0 = TIME;
// FloatOp:26
	float n_in26p1 = 0.5;
	float n_out26p0 = n_out10p0 * n_in26p1;
// FloatFunc:9
	float n_out9p0 = sin(n_out26p0);
// FloatFunc:12
	float n_out12p0 = abs(n_out9p0);
// Mix:29
	float n_in29p0 = -1.0;
	float n_in29p1 = 1.0;
	float n_out29p0 = mix(n_in29p0, n_in29p1, n_out12p0);
// VectorOp:77
	vec3 n_out77p0 = vec3(n_out32p0) + vec3(n_out29p0);
// FloatFunc:80
	float n_out80p0 = min(max(n_out77p0.x, 0.0), 1.0);
// FloatOp:8
	float n_out8p0 = n_out7p1 - n_out80p0;
// FloatFunc:13
	float n_out13p0 = 1.0 - n_out8p0;
// FloatParameter:15
	float n_out15p0 = FloatParameter;
// FloatOp:14
	float n_out14p0 = pow(n_out13p0, n_out15p0);
// VectorOp:17
	vec4 n_out17p0 = n_out16p0 * vec4(n_out14p0);
// Input:81
	vec4 n_out81p0 = COLOR;
// VectorOp:75
	vec4 n_out75p0 = n_out17p0 + n_out81p0;
// FloatFunc:11
	float n_out11p0 = ceil(n_out8p0);
// Output:0
	vec4 color_dissolove = vec4(vec3(n_out75p0.xyz), n_out11p0);
	COLOR = mix(color_dissolove, color_, isTransparency);
}
