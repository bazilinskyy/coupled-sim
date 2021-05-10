%% AnalyzePhasesGroup
% Author: Johnson Mok

function out = analyzePhasesGroup(data)
% Group data based on conditions.
out.orgdata = getOrganizedDY(data);

% Calculate each respective means and std
out.grouped = calcPosData(out.orgdata);

% Calculate phase borders
out.borders = calcAllBorders(out.grouped);
end

%% Helper functions
function out = getOrganizedDY(data)
fld = fieldnames(data.Data_ED_0.HostFixedTimeLog);
for i=1:length(fld)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0.(fld{i}) = data.Data_ED_0.HostFixedTimeLog.(fld{i});
    ND_Y.map1.(fld{i}) = data.Data_ED_4.HostFixedTimeLog.(fld{i});
    ND_Y.map2.(fld{i}) = data.Data_ED_8.HostFixedTimeLog.(fld{i});
    % ND_NY: ED 1, 5, 9
    ND_NY.map0.(fld{i}) = data.Data_ED_1.HostFixedTimeLog.(fld{i});
    ND_NY.map1.(fld{i}) = data.Data_ED_5.HostFixedTimeLog.(fld{i});
    ND_NY.map2.(fld{i}) = data.Data_ED_9.HostFixedTimeLog.(fld{i});
    % D_Y: ED 2, 6, 10
    D_Y.map0.(fld{i}) = data.Data_ED_2.HostFixedTimeLog.(fld{i});
    D_Y.map1.(fld{i}) = data.Data_ED_6.HostFixedTimeLog.(fld{i});
    D_Y.map2.(fld{i}) = data.Data_ED_10.HostFixedTimeLog.(fld{i});
    % D_NY: ED 1, 5, 9
    D_NY.map0.(fld{i}) = data.Data_ED_3.HostFixedTimeLog.(fld{i});
    D_NY.map1.(fld{i}) = data.Data_ED_7.HostFixedTimeLog.(fld{i});
    D_NY.map2.(fld{i}) = data.Data_ED_11.HostFixedTimeLog.(fld{i});
end
out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end      

function out = groupPos(data)
fld_map = fieldnames(data);
for m=1:length(fld_map)
    fld_phase = fieldnames(data.(fld_map{m}).pos);
    for k=1:length(fld_phase) 
        [max_size, ~] = max(cellfun('size', data.(fld_map{m}).pos.(fld_phase{k}), 1));
%         test = cellfun('size', data.(fld_map{m}).pos.(fld_phase{k}), 1);
%         [GC, GR] = groupcounts(test')
        pos_arr = zeros(max_size,length(data.(fld_map{m}).pos.(fld_phase{k})));
        for i =1:length(data.(fld_map{m}).pos.(fld_phase{k}))
            % fill up array
            if(length(data.(fld_map{m}).pos.(fld_phase{k}){i}(:,2))<max_size)
                for j=length(data.(fld_map{m}).pos.(fld_phase{k}){i}(:,2))+1:max_size
                    data.(fld_map{m}).pos.(fld_phase{k}){i}(j,2) = data.(fld_map{m}).pos.(fld_phase{k}){i}(end,2);
                end
            end
            % Group into one matrix
            pos_arr(:,i) = data.(fld_map{m}).pos.(fld_phase{k}){i}(:,2);
        end
        out.(fld_map{m}).(fld_phase{k})= pos_arr;
    end
end
end
function [meanData, stdData] = meanStdPos(data)
fld_map = fieldnames(data);
for i = 1:length(fld_map)
    fld_phase = fieldnames(data.(fld_map{i}));
    for j = 1:length(fld_phase)
        meanData.(fld_map{i}).(fld_phase{j}) = mean(data.(fld_map{i}).(fld_phase{j}),2);
        stdData.(fld_map{i}).(fld_phase{j}) = std(data.(fld_map{i}).(fld_phase{j}),0,2);
    end
end
end
function out = calcPosData(data)
fld = fieldnames(data);
for i = 1:length(fld)
    out.(fld{i}).grpos = groupPos(data.(fld{i}));
    [out.(fld{i}).meanData, out.(fld{i}).stdData] = meanStdPos(out.(fld{i}).grpos);
end
end

function out = calcBorders(data, con, mapping)
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}).grpos);
m = find(strcmp(fld_map,mapping));
fld_phase = fieldnames(data.(fld_con{c}).grpos.(fld_map{m}));
% Mid
temp = 0;
start_ = zeros(size(fld_phase));
half_add = zeros(size(fld_phase));
rect = zeros(length(fld_phase),4);
for i = 1:length(fld_phase)
     y = data.(fld_con{c}).grpos.(fld_map{m}).(fld_phase{i});
     start_(i) = temp;
     rect(i,:) = [temp, -10, size(y,1)*0.0167, 70];
     temp = temp+size(y,1)*0.0167;
     half_add(i) = size(y,1)*0.0167/2;
end
out.midx = start_+half_add;
out.rect = rect;
end
function out = calcAllBorders(data)
out = [];
fld_con = fieldnames(data);
for c=1:length(fld_con)
    if(~isfield(out,(fld_con{c})))
    	out.(fld_con{c}) = [];
    end
    fld_map = fieldnames(data.(fld_con{c}).grpos);
    for m=1:length(fld_map)
        if(~isfield(out.(fld_con{c}),(fld_map{m})))
            out.(fld_con{c}).(fld_map{m}) = [];
        end 
    out.(fld_con{c}).(fld_map{m}) = calcBorders(data, (fld_con{c}), (fld_map{m}));
    end
end
end

