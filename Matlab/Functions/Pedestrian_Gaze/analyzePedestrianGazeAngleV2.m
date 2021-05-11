%% Analyze Pedestrian Gaze Angle
% Angle 0-90 degree is right side of pedestrian
% 90-180 is left side of the pedestrian
% Author: Johnson Mok

function out = analyzePedestrianGazeAngleV2(origin, dir, phases, trialorder)
origin_p = getAllPhase(origin,phases);
dir_p = getAllPhase(dir,phases);
    
angles = calcAllAngle(origin_p, dir_p);
meanAngles = calcAllMeanAngle(angles);

org = getOrganizedDY(meanAngles);
org_ind = getOrganizedDY(angles);

out.ind = calcGroupCountsInd(org_ind);
out.mean = calcGroupCounts(org);

%% Statistical analysis
anglePP = meanAnglePerPerson(org_ind, trialorder);
SPSS = SPSSmatrix(anglePP);
D_D_NY = CohensD(SPSS.D_NY);
D_D_Y = CohensD(SPSS.D_Y);
D_ND_NY = CohensD(SPSS.ND_NY);
D_ND_Y = CohensD(SPSS.ND_Y);

t_D_NY = pairedSamplesttest(SPSS.D_NY);
t_D_Y = pairedSamplesttest(SPSS.D_Y);
t_ND_NY = pairedSamplesttest(SPSS.ND_NY);
t_ND_Y = pairedSamplesttest(SPSS.ND_Y);

% Table
out.StatisticalAnalysis_yaw_D_NY = getTableTtest(t_D_NY);
out.StatisticalAnalysis_yaw_D_Y = getTableTtest(t_D_Y);
out.StatisticalAnalysis_yaw_ND_NY = getTableTtest(t_ND_NY);
out.StatisticalAnalysis_yaw_ND_Y = getTableTtest(t_ND_Y);

out.StatisticalAnalysis_yaw_Cohen = getTableCohen(D_D_NY, D_D_Y, D_ND_NY, D_ND_Y);

end

%% Helper functions
function out = getPhase(data,idx)
fld_xyz = fieldnames(data);
for a=1:length(fld_xyz)
    for i=1:length(idx)
        data.(fld_xyz{a}){i} = data.(fld_xyz{a}){i}(idx{i}(1,1):idx{i}(2,size(idx{1},2)));
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

function out = calcGroupCountsInd(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        temp = cell2mat(data.(fld_con{c}).(fld_map{m}));
        [freq, out.(fld_con{c}).(fld_map{m}).val] = groupcounts(round(temp,0));
        out.(fld_con{c}).(fld_map{m}).freq = 100*freq/length(temp);
    end
end
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

function out = meanAnglePerPerson(data, order)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        A = data.(fld_con{c}).(fld_map{m});
        B = order.(fld_con{c}).(fld_map{m}).Pnr;
        for i=1:max(B)
            temp = A(find(B==i))';
            sizes = cellfun(@length, temp);
            if (min(sizes) ~= max(sizes))
                for k = 1:length(sizes)
                    if size(temp(k)) < max(sizes)
                        temp{k}(end+1:max(sizes)) = NaN;
                    end
                end
            end
            out.(fld_con{c}).(fld_map{m})(i) =  mean(mean(cell2mat(temp),2,'omitnan'),'omitnan');
        end
    end
end
end

%% Statistical analysis functions
function out = SPSSmatrix(data)
% tablename = {'SPSS_Perf_ND_Y.csv','SPSS_Perf_ND_NY.csv','SPSS_Perf_D_Y.csv','SPSS_Perf_D_NY.csv'};
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    temp.(fld_con{c}) = NaN(length(data.ND_Y.map0),3);
    if strcmp(fld_con{c},'ND_Y')
        for m=1:length(fld_map)
            temp.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m})),m) = data.(fld_con{c}).(fld_map{m});
        end
    else
        for m=1:length(fld_map)
            temp.(fld_con{c})(1:length(data.(fld_con{c}).(fld_map{m})),m) = 100-data.(fld_con{c}).(fld_map{m});
        end 
    end
    M = temp.(fld_con{c});
    T = array2table(M);
    T.Properties.VariableNames(1:3) = {'Baseline','Gaze_to_Yield','Look_Away_to_Yield'};
%     writetable(T,tablename{c})
end
out = temp;
end

function out = pairedSamplesttest(data)
[~,p1,~,stats1] = ttest(data(:,1), data(:,2));
[~,p2,~,stats2] = ttest(data(:,2), data(:,3));
[~,p3,~,stats3] = ttest(data(:,1), data(:,3));
out = zeros(3,3);
out(1,:) = [stats1.tstat, stats1.df, p1];
out(2,:) = [stats2.tstat, stats2.df, p2];
out(3,:) = [stats3.tstat, stats3.df, p3];
end
function out = CohensD(data)
pair12 = data(:,1)-data(:,2); % baseline - mapping 1
pair23 = data(:,2)-data(:,3); % mapping 1 - mapping 2
pair13 = data(:,1)-data(:,3); % baseline - mapping 2

out = zeros(3,3);
out(1,:) = calcCohen(pair12);
out(2,:) = calcCohen(pair23);
out(3,:) = calcCohen(pair13);
end
function out = calcCohen(data)
m = mean(data,'omitnan');
s = std(data,'omitnan');
D = m/s;
out = [m, s, D];
end

function T = getTableTtest(data)
% Get data
GTY_base = ['t(',num2str(data(1,2)),') = ',num2str(data(1,1)),' p = ',num2str(num2str(data(1,3)))];
LATY_base = ['t(',num2str(data(3,2)),') = ',num2str(data(3,1)),' p = ',num2str(num2str(data(3,3)))];
GTY_LATY = ['t(',num2str(data(2,2)),') = ',num2str(data(2,1)),' p = ',num2str(num2str(data(2,3)))];
% Create column data
Mapping = {'Baseline';'GTY';'LATY'};
Baseline = {'X';GTY_base;LATY_base};
GTY = {'X';'X';GTY_LATY};
LATY = {'X';'X';'X'};
% Create Table
T = table(Mapping, Baseline, GTY, LATY);
end
function T = getTableCohen(D_D_NY, D_D_Y, D_ND_NY, D_ND_Y)
% Create column data
Mapping = {'Baseline - GTY';'GTY - LATY';'Baseline - LATY'};
D_NY = D_D_NY(:,3);
D_Y = D_D_Y(:,3);
ND_NY = D_ND_NY(:,3);
ND_Y = D_ND_Y(:,3);
% Create Table
T = table(Mapping, D_NY, D_Y, ND_NY, ND_Y);
end