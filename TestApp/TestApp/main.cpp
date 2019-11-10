
#include <random>

struct point_cloud
{
	size_t size;
	float* positions;
};

template <int size>
struct static_point_cloud
{
	float positions[size][3];
};

int main()
{
	point_cloud points;
	points.size = 10;
	points.positions = new float[points.size * 3];
	for (int i = 0; i < points.size; ++i)
	{
		points.positions[i * 3 + 0] = (float)rand() / RAND_MAX * 2.f - 1.f;
		points.positions[i * 3 + 1] = (float)rand() / RAND_MAX * 2.f - 1.f;
		points.positions[i * 3 + 2] = (float)rand() / RAND_MAX * 2.f - 1.f;
	}

	static_point_cloud<5> points2;
	for (int i = 0; i < 5; ++i)
	{
		points2.positions[i][0] = (float)rand() / RAND_MAX * 2.f - 1.f;
		points2.positions[i][1] = (float)rand() / RAND_MAX * 2.f - 1.f;
		points2.positions[i][2] = (float)rand() / RAND_MAX * 2.f - 1.f;
	}

	int v = 0;
}
