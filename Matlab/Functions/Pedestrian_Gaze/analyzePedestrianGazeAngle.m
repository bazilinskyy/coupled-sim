%% Analyze Pedestrian Gaze Angle
% Angle 0-90 degree is right side of pedestrian
% 90-180 is left side of the pedestrian
% Author: Johnson Mok
% Last Updated: 24-02-2021
function out = analyzePedestrianGazeAngle(origin, dir, phases)
origin_p = getAllPhase(origin,phases);
dir_p = getAllPhase(dir,phases);
    
angles = calcAllAngle(origin_p, dir_p);
meanAngles = calcAllMeanAngle(angles);

org = getOrganizedDY(meanAngles);
out = calcGroupCounts(org);
end

%% Helper functions
function out = getPhase(data,idx)
fld_xyz = fieldnames(data);
if length(idx{1})>3
    st = 4;
else
    st = 3;
end
for a=1:length(fld_xyz)
    for i=1:length(idx)
        data.(fld_xyz{a}){i} = data.(fld_xyz{a}){i}(idx{i}(1,1):idx{i}(2,st));
        data.(fld_xyz{a}){i}(data.(fld_xyz{a}){i} == -1) = NaN;
    end
end
out = data;
end
function out = getAllPhase(data,phase)
fld_ED = fieldnames(data);
for ed=1:length(fld_ED)
    out.(fld_ED{ed}) = getPhase(data.(fld_ED{ed}).HostFixedTimeLog, phase.(fld_ED{ed}).HostFixedTimeLog.idx);
end

end

function out = calcAngle(origin, dir)
%ignore y-value
angle = cell(size(origin.x));
for i=1:length(origin.x)
    x1 = origin.x{i};
    z1 = origin.z{i};
    x2 = x1+dir.x{i};
    z2 = z1+dir.z{i};
    angle{i} =  atan2d(z2-z1,x2-x1);
end
out=angle;
end
function out = calcAllAngle(origin, dir)
fld_ED = fieldnames(dir);
for ed=1:length(fld_ED)
    out.(fld_ED{ed}) = calcAngle(origin.(fld_ED{ed}), dir.(fld_ED{ed}));
end
end

function out = calcMeanAngle(data)
% Fill up array with NaN
largest_array = max(cellfun(@length,data));
for i=1:length(data)
    if(length(data{i})<largest_array)
        for p=1:largest_array-length(data{i})
            data{i}(end+1) = NaN;
        end
    end
end
mat = cell2mat(data');
out = mean(mat,2,'omitnan');
end
function out = calcAllMeanAngle(data)
fld_ED = fieldnames(data);
for ed=1:length(fld_ED)
    out.(fld_ED{ed}) = calcMeanAngle(data.(fld_ED{ed}));
end
end

function out = getOrganizedDY(data)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0 = data.Data_ED_0;
    ND_Y.map1 = data.Data_ED_4;
    ND_Y.map2 = data.Data_ED_8;
    % ND_NY: ED 1, 5, 9
    ND_NY.map0 = data.Data_ED_1;
    ND_NY.map1 = data.Data_ED_5;
    ND_NY.map2 = data.Data_ED_9;
    % D_Y: ED 2, 6, 10
    D_Y.map0 = data.Data_ED_2;
    D_Y.map1 = data.Data_ED_6;
    D_Y.map2 = data.Data_ED_10;
    % D_NY: ED 1, 5, 9
    D_NY.map0 = data.Data_ED_3;
    D_NY.map1 = data.Data_ED_7;
    D_NY.map2 = data.Data_ED_11;
out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end  

function out = calcGroupCounts(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        [freq, out.(fld_con{c}).(fld_map{m}).val] = groupcounts(round(data.(fld_con{c}).(fld_map{m}),0));
        out.(fld_con{c}).(fld_map{m}).freq = 100*freq/length(data.(fld_con{c}).(fld_map{m}));
    end
end
end
