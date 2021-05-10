%% Visualize Learning Effect
% Author: Johnson Mok

function visualizeLearnEffect(data)
fld_con = fieldnames(data);
titlestr = {'No Distraction - Yielding', 'No Distraction - No Yielding',...
    'Distraction - Yielding', 'Distraction - No Yielding'};
visAll(data, fld_con, titlestr);
end

%% Helper function
function visAll(data, fld_con, titlestr)
figure;
for c=1:length(fld_con)
    subplot(1,4,c);
    hold on;
    grid on;
    xlabel('Trial','FontSize',15,'FontWeight','bold');
    ylabel({'Mean performance score'},'FontSize',15,'FontWeight','bold');
    title(titlestr{c},'FontSize',15,'FontWeight','bold');
    set(gca,'XTick',(1:1:4));
    set(gca,'FontSize',15);
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        plot(data.(fld_con{c}).(fld_map{m}),'o-','LineWidth',2,'MarkerSize',4);
    end
    yl = ylim;
    ylim([yl(1)-10 yl(2)+10]);
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