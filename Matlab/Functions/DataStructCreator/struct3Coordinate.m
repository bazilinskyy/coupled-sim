%% struct3Coordinate
% This script turns matrices into xyz fields
% Author: Johnson Mok

function out = struct3Coordinate(M)
out.x = M(:,1);
out.y = M(:,2);
out.z = M(:,3);
return 