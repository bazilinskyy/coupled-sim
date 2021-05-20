%% GazeAtLaser
% Calculate the time the pedestrian gazes at the passenger laser.
% Per Trial, find intersections per data point.
% Intersection points are saved in index_intersect.
% Corresponding vehicle distance between pedestrian is saved in papos.

% Laser width in unity was 0.07 m -> set tolerance to 0.035m

% Author: Johnson Mok

function out = GazeAtLaser(data_pe_org, data_pe_dir, data_pa_org, data_pa_dir, pa_pos)
out.pe_watching_laser_time = zeros(size(data_pe_org.x));
out.index_intersect = cell(size(data_pe_org.x));
out.papos = cell(size(data_pe_org.x));

for j=1:length(data_pe_org.x) % trial
    pos = pa_pos{j};
% Modified gaze origin and direction
[pe_org, pe_dirL] = GetOriginDirection(data_pe_org, data_pe_dir, j);
[pa_org, pa_dirL] = GetOriginDirection(data_pa_org, data_pa_dir, j);

P_intersect = cell(size(pe_org.x));
distances = cell(size(pe_org.x));
bool = zeros(size(pe_org.x));
for i = 1:length(pe_org.x)
    PA = [pa_org.x(i), pa_org.y(i), pa_org.z(i); pe_org.x(i), pe_org.y(i), pe_org.z(i)];
    PB = [pa_dirL.x(i), pa_dirL.y(i), pa_dirL.z(i); pe_dirL.x(i), pe_dirL.y(i), pe_dirL.z(i)];
    [P_intersect{i},distances{i}, bool(i)] = lineIntersect3D(PA,PB,0.035);
end

out.index_intersect{j} = find(bool);
% k = 1;
% if ~isempty(index_intersect{j})
%     visualiseLaser(pe_org, pe_dirL, pa_org, pa_dirL,index_intersect{j}(k),P_intersect{index_intersect{j}(k)});
% %     view(0,0) % XZ
% end
out.papos{j} = pos(out.index_intersect{j})-17.19;

%%
% gaze laser 
% [pe_x,pe_y,pe_z] = GetLaser(pe_org,pe_dirL);
% [pa_x,pa_y,pa_z] = GetLaser(pa_org,pa_dirL);

% intersection laser + angle
% tol = 0.1;
% index = cell(size(pe_x));
% for i =1:length(pe_x)
%     if(sum(isnan(pe_x{i}))||sum(isnan(pe_y{i}))||sum(isnan(pe_z{i}))||sum(isnan(pa_x{i}))||sum(isnan(pa_y{i}))||sum(isnan(pa_z{i})))
%         index{i} = 0;
%     else
% %         index{i} = abs(sum([pe_x{i}, pe_y{i}, pe_z{i}]-[pa_x{i}, pa_y{i}, pa_z{i}],2)) < tol;
%         index{i} = sqrt((pe_x{i}-pa_x{i}).^2 + (pe_y{i}-pa_y{i}).^2 + (pe_z{i}-pa_z{i}).^2 ) < tol;
%     end
% end
% intersect = cellfun(@sum,index);
% out.index_intersect{j} = find(intersect);
% out.papos{j} = pos(out.index_intersect{j})-17.19;
% out.pe_watching_laser_time(j) = nnz(intersect)*0.0167;

% visualiseLaser(pe_org, pe_dirL, pa_org, pa_dirL, out.index_intersect{j}(1))
% visualiseLaser(pe_org, pe_dirL, pa_org, pa_dirL,1)
end
end

%% Helper functions
function out = RemoveNoTracking(data)
fld_coor = fieldnames(data);
for coor = 1:length(fld_coor)
        data.(fld_coor{coor})(data.(fld_coor{coor}) == -1) = NaN;
end
out = data;
end
function out = DirectionViewPoint(org,dir,L)
fld_coor = fieldnames(dir);
for c = 1:length(fld_coor)
    out.(fld_coor{c}) = org.(fld_coor{c})+dir.(fld_coor{c})*L;
end
end
function [out_org, out_dirL] = GetOriginDirection(org, dir, j)
org_temp.x = org.x{j};
org_temp.y = org.y{j};
org_temp.z = org.z{j};
dir_temp.x = dir.x{j};
dir_temp.y = dir.y{j};
dir_temp.z = dir.z{j};
temp_pe_dir = RemoveNoTracking(dir_temp);
out_org = RemoveNoTracking(org_temp);
out_dirL = DirectionViewPoint(out_org,temp_pe_dir,100);
end
function out = createLaser(org, dir)
if(isnan(org)||isnan(dir))
    out = NaN;
else
    out = linspace(org,dir);
end
end
function [x,y,z] = GetLaser(org,dir)
x = cell(size(org.x));
y = cell(size(org.x));
z = cell(size(org.x));

for i=1:length(org.x)
    x{i} = createLaser(org.x(i),dir.x(i))'; 
    y{i} = createLaser(org.y(i),dir.y(i))'; 
    z{i} = createLaser(org.z(i),dir.z(i))'; 
end
end

function visualiseLaser(pe_org, pe_dirL, pa_org, pa_dirL, i, PInt)
figure;
hold on;
grid on;
plot3([pe_org.x(i), pe_dirL.x(i)], [pe_org.y(i), pe_dirL.y(i)], [pe_org.z(i), pe_dirL.z(i)], 'b')
plot3(pe_org.x(i), pe_org.y(i), pe_org.z(i),'bo');
plot3([pa_org.x(i), pa_dirL.x(i)], [pa_org.y(i), pa_dirL.y(i)], [pa_org.z(i), pe_dirL.z(i)],'r')
plot3(pa_org.x(i), pa_org.y(i), pa_org.z(i),'ro')
plot3(PInt(1), PInt(2), PInt(3), 'ko')
view(3);
xlabel('x')
ylabel('y')
zlabel('z')
title('blue is pedestrian');

end