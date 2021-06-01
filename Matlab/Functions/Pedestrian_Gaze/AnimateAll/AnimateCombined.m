%% AnimateAllTrials
% Animate all trials of condition and mapping combined into one figure.
% This script prepares the data for the animation.
% Hierarchy: AnimateCombined -> InputCombined -> AnimateTrialsCombined
% Author: Johnson Mok

function AnimateCombined(gap, pa_Dir, pa_Org, pe_Dir, pe_Org, di_pos)
paDir = getOrganizedDY(pa_Dir);
paOrg = getOrganizedDY(pa_Org);
peDir = getOrganizedDY(pe_Dir);
peOrg = getOrganizedDY(pe_Org);
diOrg = getOrganizedDY(di_pos);

paDirNan = RemoveNoTracking(paDir);
paOrgNan = RemoveNoTracking(paOrg);
peDirNan = RemoveNoTracking(peDir);
peOrgNan = RemoveNoTracking(peOrg);
diOrgNan = RemoveNoTracking(diOrg);

diOrgNanRange = RemoveDataAfterLeavingScene(diOrgNan);

paDirL = DirectionViewPoint(paOrgNan,paDirNan,75);
peDirL = DirectionViewPoint(peOrgNan,peDirNan,50);

paDirMat = combineCellsToMatrix(paDirL);
paOrgMat = combineCellsToMatrix(paOrgNan);
peDirMat = combineCellsToMatrix(peDirL);
peOrgMat = combineCellsToMatrix(peOrgNan);
diMat = combineCellsToMatrix(diOrgNanRange);

paDirMatMean = meanStdLaser(paDirMat);
paOrgMatMean = meanStdLaser(paOrgMat);
peDirMatMean = meanStdLaser(peDirMat);
peOrgMatMean = meanStdLaser(peOrgMat);


fld_con = fieldnames(paDirMat);
for c=1:length(fld_con)
    fld_map = fieldnames(paDirMat.(fld_con{c}));
    for m=1:length(fld_map)
        titlestring = getTitle(fld_con{c},fld_map{m});
        
        InputCombined(gap.sumGap.(fld_con{c}).(fld_map{m}).gapAcceptance,...
            paDirMat.(fld_con{c}).(fld_map{m}), ...
            paOrgMat.(fld_con{c}).(fld_map{m}),...
            peDirMat.(fld_con{c}).(fld_map{m}),...
            peOrgMat.(fld_con{c}).(fld_map{m}),...
            paDirMatMean.(fld_con{c}).(fld_map{m}), ...
            paOrgMatMean.(fld_con{c}).(fld_map{m}),...
            peDirMatMean.(fld_con{c}).(fld_map{m}),...
            peOrgMatMean.(fld_con{c}).(fld_map{m}),...
            diMat.(fld_con{c}).(fld_map{m}),...
            titlestring);
    end
end
end

%% Helper functions
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
function out = RemoveNoTracking(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog);
        for coor = 1:length(fld_coor)
            if(iscell(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor})))
                for i=1:length(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}))
                    data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i}(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i} == -1) = NaN;
                end
            end
        end
    end
end
out = data;
end

function out = RemoveDataAfterLeavingScene(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog);
        for coor = 1:length(fld_coor)
            if(iscell(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor})))
                for i=1:length(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}))
                    data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i}(800:end) = NaN;
                end
            end
        end
    end
end
out = data;
end

function out = DirectionViewPoint(org,dir,L)
fld_con = fieldnames(dir);
for c=1:length(fld_con)
    fld_map = fieldnames(dir.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(dir.(fld_con{c}).(fld_map{m}).HostFixedTimeLog);
        for coor = 1:length(fld_coor)
            for i=1:length(dir.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}))
                out.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i} = org.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i}+dir.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor}){i}*L;
            end
        end
    end
end
end

function out = combineCellsToMatrix(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog);
        for coor = 1:length(fld_coor)
            if(iscell(data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor})))
                mat = data.(fld_con{c}).(fld_map{m}).HostFixedTimeLog.(fld_coor{coor})';
                out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = padcat(mat{:});
            else
                out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = nan;
            end
        end
    end
end
end
function out = getTitle(con, map)

if(strcmp(con,'ND_Y'))
    c = 'ND-Y';
elseif(strcmp(con,'ND_NY'))
    c = 'ND-NY';
elseif(strcmp(con,'D_Y'))
    c = 'D-Y';
elseif(strcmp(con,'D_NY'))
    c = 'D-NY';
end

if(strcmp(map,'map0'))
    m = 'baseline';
elseif(strcmp(map,'map1'))
    m = 'GTY';
elseif(strcmp(map,'map2'))
    m = 'LATY';
end

out = [m,'-',c];
end

function out = meanStdLaser(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for coor = 1:length(fld_coor)
            data_ = data.(fld_con{c}).(fld_map{m}).(fld_coor{coor});
            if coor == 2
                data_ = data_ - 2; % To plot the mean above the other lasers.
            end
            M = mean(data_,2,'omitnan');
            S = std(data_,0,2,'omitnan');
%             P = prctile(data_,[25 75],2);
            out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = [M, M-S, M+S];        
        end
    end
end
end