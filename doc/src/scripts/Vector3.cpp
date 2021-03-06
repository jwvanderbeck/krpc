#include <krpc.hpp>
#include <krpc/services/space_center.hpp>
#include <iostream>

using namespace krpc::services;

int main() {
  krpc::Client conn = krpc::connect();
  SpaceCenter sc(&conn);
  std::tuple<double,double,double> v = sc.active_vessel().flight().prograde();
  std::cout << std::get<0>(v) << " "
            << std::get<1>(v) << " "
            << std::get<2>(v) << std::endl;
}
