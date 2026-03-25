/** @type {import('next').NextConfig} */
const nextConfig = {
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: 'http://localhost:5001/api/:path*',
      },
      {
        source: '/hubs/:path*',
        destination: 'http://localhost:5001/hubs/:path*',
      },
    ];
  },
};

export default nextConfig;
