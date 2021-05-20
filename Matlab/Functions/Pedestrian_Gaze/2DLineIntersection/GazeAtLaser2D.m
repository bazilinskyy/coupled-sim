%% GazeAtLaser2D
% Calculate the intersection of pedestrian laser and driver laser on the xz
% plane.

% Author: Johnson Mok

function out = GazeAtLaser2D(data)
fld_ED = fieldnames(data);
for ed=1:length(fld_ED)
    fld_part = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
    for p=1:length(fld_part)
        fld_trial = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}));
        for tr=1:length(fld_trial)
            % Get relevant data
            input = data.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}).(fld_trial{tr});
            pe_org = input.pe.world.gaze_origin;
            pe_dir = input.pe.world.gaze_dir;
            pa_org = input.pa.world.gaze_origin;
            pa_dir = input.pa.world.gaze_dir;
            
            % Preprocess data
            pe_org_ = RemoveNoTracking(pe_org);
            pe_dir_ = RemoveNoTracking(pe_dir);
            pa_org_ = RemoveNoTracking(pa_org);
            pa_dir_ = RemoveNoTracking(pa_dir);
            
            pe_vp = DirectionViewPoint(pe_org_,pe_dir_,100); % vp = viewpoint
            pa_vp = DirectionViewPoint(pa_org_,pa_dir_,100);
            
            % Calc line intersection per data point
            inter = zeros(length(pe_org_.x),1);
            for i=1:length(pe_org_.x)
                lines_input = [pe_org_.x(i), pe_org_.z(i); ...
                    pe_vp.x(i), pe_vp.z(i); ...
                    pa_org_.x(i), pa_org_.z(i);...
                    pa_vp.x(i), pa_vp.z(i)];
                inter(i) = InterX(lines_input);
            end
            out.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}).(fld_trial{tr}) = inter;
        end
    end
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

