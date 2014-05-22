Discovery module for NDNMOG

Updated May 21, 2014

This is the first tested with multiple machines version of DiscoveryModule.

Based on NDN-CCL's DotNet implementation by zhehao. Its code accessible at:
https://github.com/zhehaowang/ndn-dot-net
The dotnet CCL is based on and (almost) consistent with jndn, accessible at:
https://github.com/named-data/jndn

Tested to work with ndnd-tlv, accessible at:
https://github.com/named-data/ndnd-tlv
Note that the official support does not mention that ndnd-tlv does not compile with the latest dependency ndn-cxx at:
https://github.com/named-data/ndn-cxx
So it is advised to clone ndn-cxx, and revert to a late April commit.

Current implementation include:
Broadcast sync-style discovery message
Position update based on 1 second freshness period

Future updates will focus on:
Decision strategy for which octant to express interest towards
Deciding when to express interest towards higher level octant
Devise a different scheme for position update

See the DiscoveryModule in action at:
https://github.com/zhehaowang/NDNMOG-live

wangzhehao410305@gmail.com
