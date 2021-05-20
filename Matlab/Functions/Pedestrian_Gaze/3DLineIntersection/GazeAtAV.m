%% GazeAtAV
% Calculate the time the pedestrian gazes at the AV
% Author: Johnson Mok

function out = GazeAtAV(data)
data_clean = RemoveNoTracking(data);
data_uniq = GetUniqueRound(data_clean,0);
data_col = cat(1, data_uniq{:});
[freq, val] = groupcounts(data_col);
% Remove zero
idx_zero = find(val==0);
freq(idx_zero) = [];
val(idx_zero) = [];

out.freq = freq;
out.val = val;
end

%% Helper functions
function out = RemoveNoTracking(data)
for i=1:length(data)
%     data{i}(data{i} == -8) = 0; % Change no tracking to NaN
    data{i}(data{i} < 0) = 0; % Clean data
end
out = data;
end

function out = GetUniqueRound(data,r)
out = cell(size(data));
for i=1:length(data)
    out{i} = unique(round(data{i},r));
end
end