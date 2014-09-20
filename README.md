Discovery module for NDNMOG

Updated Sept 19, 2014

Discovery module works with NDN-CCL's DotNet implementation by zhehao. Its code accessible at:
https://github.com/zhehaowang/ndn-dot-net

(which is an adaptation of Java CCL code: https://github.com/named-data/jndn)

Discovery module works with ndnd-tlv and nfd.

Current implementation include:
* Octant partitioning and indexing
* Octant based broadcast sync-style discovery message
* Position update based on incrementing sequence number, at a rate of 25Hz

Future updates will focus on:
Implementation of progressive discovery, which is supported in the module, but not utilized by the test in Unity yet.

See the DiscoveryModule in action at:
https://github.com/remap/ndn-mog/tree/master

wangzhehao410305@gmail.com
