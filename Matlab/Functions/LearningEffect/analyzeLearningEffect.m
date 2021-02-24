%% Analyze Learning Effect
% Author: Johnson Mok
% Last Updated: 24-02-2021
function learnEffect = analyzeLearningEffect(data)
org = getOrganizedDY(data);
learnEffect = meanScore(org);
end

%% Helperfunctions
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

function out = calcMeanScore(data)
som = zeros(size(data));
for i=1:length(data)
    som(i) = 100*sum(data{i})/length(data{i});
end
out = mean(som);
end
function out = meanScore(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_trial = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for tr=1:length(fld_trial)
            out.(fld_con{c}).(fld_map{m})(tr) = calcMeanScore(data.(fld_con{c}).(fld_map{m}).(fld_trial{tr}));
        end
    end
end

end