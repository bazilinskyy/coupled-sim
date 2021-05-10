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
end

