%% Visualize Learning Effect
% Author: Johnson Mok
% Last Updated: 24-02-2021

function visualizeLearnEffect(data)
fld_con = fieldnames(data);
titlestr = {'No distraction - Yielding', 'No distraction - No yielding',...
    'Distraction - Yielding', 'Distraction - No yielding'};
visAll(data, fld_con, titlestr);
visOne(data, fld_con, titlestr, 1);
end

%% Helper function
function visAll(data, fld_con, titlestr)
figure;
for c=1:length(fld_con)
    subplot(2,2,c);
    hold on;
    grid on;
    xlabel('Trial','FontSize',15,'FontWeight','bold');
    ylabel({'Mean crossing';'performance in [%]'},'FontSize',15,'FontWeight','bold');
    title(titlestr{c},'FontSize',15,'FontWeight','bold');
    ylim([40 90]);
    set(gca,'XTick',(1:1:4));
    set(gca,'FontSize',15);
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        plot(data.(fld_con{c}).(fld_map{m}),'o-','LineWidth',2,'MarkerSize',4);
    end
    legend('baseline','Gaze to yield','Look away to yield','Location','southwest','FontSize',15,'FontWeight','bold');
end
end
function visOne(data, fld_con, titlestr, c)
figure;
    hold on;
    grid on;
    xlabel('Trial','FontSize',15,'FontWeight','bold');
    ylabel({'Mean crossing';'performance in [%]'},'FontSize',15,'FontWeight','bold');
    title(titlestr{c},'FontSize',15,'FontWeight','bold');
    ylim([40 90]);
    set(gca,'XTick',(1:1:4));
    set(gca,'FontSize',15);
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        plot(data.(fld_con{c}).(fld_map{m}),'o-','LineWidth',2,'MarkerSize',4);
    end
    legend('baseline','Gaze to yield','Look away to yield','Location','southwest','FontSize',15,'FontWeight','bold');
end