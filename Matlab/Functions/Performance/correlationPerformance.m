%% Correlation between performance score and decision reversals
% Author: Johnson Mok

function out = correlationPerformance(score,dc)
fld_map = {'map0';'map1';'map2'};
fld_con = fieldnames(dc);
for c=1:length(fld_con)
    [out.R_all.(fld_con{c}), out.P_all.(fld_con{c})] = corrcoef(dc.(fld_con{c}),score.(fld_con{c}));
end

% Per mapping
for c=1:length(fld_con)
    for i=1:3
        [out.R_map.(fld_con{c}).(fld_map{i}), out.P_map.(fld_con{c}).(fld_map{i})] = corrcoef(dc.(fld_con{c})(:,i),score.(fld_con{c})(:,i));
    end
end

% table
% Create column data
Mapping = {'Baseline';'GTY';'LATY';'All'};
D_NY = getTableString(out.R_map.D_NY, out.R_all.D_NY, out.P_map.D_NY, out.P_all.D_NY);
D_Y = getTableString(out.R_map.D_Y, out.R_all.D_Y, out.P_map.D_Y, out.P_all.D_Y);
ND_NY = getTableString(out.R_map.ND_NY, out.R_all.ND_NY, out.P_map.ND_NY, out.P_all.ND_NY);
ND_Y = getTableString(out.R_map.ND_Y, out.R_all.ND_Y, out.P_map.ND_Y, out.P_all.ND_Y);

% Create Table
out.Correlation_crossPerformance_DC = table(Mapping, D_NY, D_Y, ND_NY, ND_Y);
end

%% Helperfunction 
function out = getTableString(R_map, R_all, P_map, P_all)
out = {[num2str(R_map.map0(2)),' (',num2str(P_map.map0(2)),')']; [num2str(R_map.map1(2)),' (',num2str(P_map.map1(2)),')'];...
   [num2str(R_map.map2(2)),' (',num2str(P_map.map2(2)),')']; [num2str(R_all(2)),' (',num2str(P_all(2)),')']};
end
