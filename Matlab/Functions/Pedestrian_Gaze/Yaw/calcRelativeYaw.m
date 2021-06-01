%% CalcRelativeYaw
% Calculate yaw difference between the pedestrian gaze and the angle
% between the pedestrian and AV position from topview.
% Author: Johnson Mok
function out = calcRelativeYaw(angle, pos_pe, pos_pa, phases)
% Get start till end analysis period
pos_pe_p = getAllPhase(pos_pe,phases);
pos_pa_p = getAllPhase(pos_pa,phases);
% Calculate angle
AVyaw = calcAVYaw(pos_pe_p, pos_pa_p);
anglePerPhase = getPerPhase(AVyaw,phases);
anglePerPhase_org = getOrganizedDY(anglePerPhase);
diffyaw = diffYaw(angle, anglePerPhase_org);
%
out.yawPerPhase = combineCellsToMatrix(diffyaw);
%
out.CountPerPhase = calcGroupCounts(out.yawPerPhase);



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

function out = calcAngle(origin, dir)
%ignore y-value
angle = cell(size(origin.x));
for i=1:length(origin.x)
    x1 = origin.x{i};
    z1 = origin.z{i};
    x2 = dir.x{i};
    z2 = dir.z{i};
    angle{i} = atan2d(z2-z1,x2-x1)+90;
end
    %% debug
    if(false)
    figure
    subplot(1,2,1)
    hold on;
    for j=1:length(x1)
        plot([x1(j) x2(j)],[z1(j), z2(j)]);
        title(angle{i}(j));
    end
    subplot(1,2,2)
    hold on;
    plot(angle{i});
    yline(mean(angle{i},'omitnan'));
    end
out=angle;
end

function out = calcAVYaw(origin, dir)
fld_ED = fieldnames(dir);
for ed=1:length(fld_ED)
    out.(fld_ED{ed}) = calcAngle(origin.(fld_ED{ed}), dir.(fld_ED{ed}));
end
end

function out = diffYaw(gazeyaw, avyaw)
fld_con = fieldnames(gazeyaw);
for c=1:length(fld_con)
    fld_map = fieldnames(gazeyaw.(fld_con{c}));
    for m=1:length(fld_map)
        fld_phase = fieldnames(gazeyaw.(fld_con{c}).(fld_map{m}));
        for p=1:length(fld_phase)
            for i=1:length(avyaw.(fld_con{c}).(fld_map{m}).(fld_phase{p}))
                out.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i} = gazeyaw.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i}-avyaw.(fld_con{c}).(fld_map{m}).(fld_phase{p}){i};
            end
        end
    end
end
end

function out = combineAngles(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = rmmissing(cat(1, data.(fld_con{c}).(fld_map{m}){:})); 
    end
end
end

function out = getPerPhase(angle,phases)
% phase.ed.time.idx{i}
% angle.ed{i}
fld_ed = fieldnames(angle);
for ed=1:length(fld_ed)
    for i =1:length(angle.(fld_ed{ed}))
        ID = phases.(fld_ed{ed}).HostFixedTimeLog.idx{i};
        out.(fld_ed{ed}).ph1{i} = angle.(fld_ed{ed}){i}(ID(1,1):ID(2,1));
        out.(fld_ed{ed}).ph2{i} = angle.(fld_ed{ed}){i}(ID(1,2):ID(2,2));
        if (size(phases.(fld_ed{ed}).HostFixedTimeLog.idx{i},2)>2)
            out.(fld_ed{ed}).ph3{i} = angle.(fld_ed{ed}){i}(ID(1,3):ID(2,3));
        end
    end
end

end

function out = combineCellsToMatrix(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_coor = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for coor = 1:length(fld_coor)
            if(iscell(data.(fld_con{c}).(fld_map{m}).(fld_coor{coor})))
                mat = data.(fld_con{c}).(fld_map{m}).(fld_coor{coor})';
%                 out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = padcat(mat{:});
                out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = cat(1, mat{:});
            else
                out.(fld_con{c}).(fld_map{m}).(fld_coor{coor}) = nan;
            end
        end
    end
end
end

function out = calcGroupCounts(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for p=1:length(fld_phase)
            [freq, out.(fld_con{c}).(fld_map{m}).(fld_phase{p}).val] = groupcounts(round(data.(fld_con{c}).(fld_map{m}).(fld_phase{p})/1)*1); %groupcounts(round(data.(fld_con{c}).(fld_map{m}),0));
            out.(fld_con{c}).(fld_map{m}).(fld_phase{p}).freq = 100*freq/length(data.(fld_con{c}).(fld_map{m}).(fld_phase{p}));
            disp(join([(fld_con{c}),' ',(fld_map{m}),' ',(fld_phase{p}),': ',num2str(length(data.(fld_con{c}).(fld_map{m}).(fld_phase{p})))]));
        end
    end
end
end